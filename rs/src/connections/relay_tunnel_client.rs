// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::{
    env, io,
    net::{IpAddr, Ipv4Addr, SocketAddr},
    pin::Pin,
    sync::Arc,
    task::Poll,
    time::Duration,
};

use crate::{contracts::TunnelEndpoint, management::TunnelManagementClient};
use async_trait::async_trait;
use russh::ChannelMsg;
use tokio::{
    io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt},
    net::TcpListener,
    sync::watch,
};

use super::{
    errors::TunnelError,
    ws::{build_websocket_request, connect_directly, connect_via_proxy, AsyncRWWebSocket},
};

/// The RelayTunnelClient connects to a tunnel as a client via the tunneling
/// service's relay. After connecting, you can open connections to individual
/// forwarded ports via `connect_to_port`, or forward them to local TCP
/// listeners via `forward_port_locally`.
///
/// # Interoperability
///
/// This client uses SSH `direct-tcpip` channels to connect to forwarded ports.
/// This works with Rust and TypeScript/C# tunnel hosts. Go tunnel hosts do
/// not currently handle `direct-tcpip` channels and will reject connections
/// from this client.
///
/// # Example
///
/// ```ignore
/// let mgmt = TunnelManagementClient::new(/* ... */);
/// let client = RelayTunnelClient::new(mgmt);
///
/// // endpoint and token are obtained from the tunnel management API
/// let handle = client.connect(&endpoint, "access_token").await?;
///
/// // Connect to port 8080 on the tunnel host
/// let channel = handle.connect_to_port(8080).await?;
///
/// // Or forward a remote port to a local TCP listener
/// let local_addr = handle.forward_port_locally(8080).await?;
/// ```
pub struct RelayTunnelClient {
    pub proxy: Option<String>,
    mgmt: TunnelManagementClient,
}

impl RelayTunnelClient {
    pub fn new(mgmt: TunnelManagementClient) -> Self {
        RelayTunnelClient {
            proxy: env::var("HTTPS_PROXY").or(env::var("https_proxy")).ok(),
            mgmt,
        }
    }

    /// Connects to the tunnel relay as a client. The endpoint must have a
    /// `client_relay_uri`, and the `access_token` should be a tunnel access
    /// token with the "connect" scope.
    ///
    /// Returns a `ClientRelayHandle` that can be used to open port connections.
    pub async fn connect(
        &self,
        endpoint: &TunnelEndpoint,
        access_token: &str,
    ) -> Result<ClientRelayHandle, TunnelError> {
        let client_relay_uri = endpoint
            .tunnel_relay_tunnel_endpoint
            .client_relay_uri
            .as_deref()
            .ok_or(TunnelError::MissingClientEndpoint)?;

        let req = build_websocket_request(
            client_relay_uri,
            &[
                ("Sec-WebSocket-Protocol", "tunnel-relay-client"),
                ("Authorization", &format!("tunnel {}", access_token)),
                ("User-Agent", self.mgmt.user_agent.to_str().unwrap()),
            ],
        )?;

        let cnx = if let Some(proxy) = &self.proxy {
            log::debug!("connecting via http_proxy on {}", proxy);
            connect_via_proxy(req, proxy).await?
        } else {
            connect_directly(req).await?
        };

        let cnx = AsyncRWWebSocket::new(super::ws::AsyncRWWebSocketOptions {
            websocket: cnx,
            ping_interval: Duration::from_secs(60),
            ping_timeout: Duration::from_secs(10),
        });

        let mut session = Self::make_ssh_session(cnx)
            .await
            .map_err(TunnelError::TunnelRelayDisconnected)?;

        log::debug!("established client relay session");

        let authed = session
            .authenticate_none("tunnel")
            .await
            .map_err(TunnelError::TunnelRelayDisconnected)?;

        if !authed {
            return Err(TunnelError::TunnelRelayDisconnected(
                russh::Error::NotAuthenticated,
            ));
        }

        log::debug!("client relay session authenticated");

        let (close_tx, close_rx) = watch::channel(false);
        Ok(ClientRelayHandle {
            session: Arc::new(session),
            close_tx,
            close_rx,
        })
    }

    async fn make_ssh_session(
        rw: impl AsyncRead + AsyncWrite + Unpin + Send + 'static,
    ) -> Result<russh::client::Handle<TunnelClientHandler>, russh::Error> {
        let config = russh::client::Config {
            window_size: 1024 * 1024,
            preferred: russh::Preferred {
                compression: &["none"],
                ..russh::Preferred::DEFAULT
            },
            limits: russh::Limits {
                rekey_read_limit: usize::MAX,
                rekey_time_limit: Duration::MAX,
                rekey_write_limit: usize::MAX,
            },
            ..Default::default()
        };

        let config = Arc::new(config);
        let handler = TunnelClientHandler;
        russh::client::connect_stream(config, rw, handler).await
    }
}

/// Handle to an active client relay connection. Use this to open connections
/// to forwarded ports on the tunnel host.
pub struct ClientRelayHandle {
    session: Arc<russh::client::Handle<TunnelClientHandler>>,
    close_tx: watch::Sender<bool>,
    close_rx: watch::Receiver<bool>,
}

impl ClientRelayHandle {
    /// Opens a connection to the specified port on the tunnel host, returning
    /// a `PortConnection` that can be used to send and receive data.
    pub async fn connect_to_port(&self, port: u16) -> Result<PortConnection, TunnelError> {
        let channel = self
            .session
            .channel_open_direct_tcpip("127.0.0.1", port as u32, "127.0.0.1", 0)
            .await
            .map_err(TunnelError::TunnelRelayDisconnected)?;
        Ok(PortConnection { channel })
    }

    /// Opens a local TCP listener that forwards connections to the specified
    /// port on the tunnel host. Returns the local address the listener is
    /// bound to (useful when the requested port is already in use and a
    /// fallback port was chosen).
    ///
    /// The listener will be stopped when `close()` is called on this handle.
    pub async fn forward_port_locally(&self, port: u16) -> Result<SocketAddr, TunnelError> {
        let addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::LOCALHOST), port);
        let listener = match TcpListener::bind(addr).await {
            Ok(l) => l,
            Err(e) => {
                log::warn!(
                    "Failed to bind port {} ({}), falling back to ephemeral port",
                    port,
                    e
                );
                TcpListener::bind(SocketAddr::new(IpAddr::V4(Ipv4Addr::LOCALHOST), 0))
                    .await
                    .map_err(TunnelError::ProxyConnectionFailed)?
            }
        };

        let local_addr = listener
            .local_addr()
            .map_err(TunnelError::ProxyConnectionFailed)?;

        let session = self.session.clone();
        let mut close_rx = self.close_rx.clone();
        tokio::spawn(async move {
            log::info!("Listening on {} for forwarded port {}", local_addr, port);
            loop {
                tokio::select! {
                    result = listener.accept() => match result {
                        Ok((stream, peer)) => {
                            log::debug!("Accepted connection from {} for port {}", peer, port);
                            let session = session.clone();
                            tokio::spawn(async move {
                                if let Err(e) = relay_tcp_to_channel(stream, &session, port).await {
                                    log::debug!("Error relaying connection on port {}: {}", port, e);
                                }
                            });
                        }
                        Err(e) => {
                            log::info!("Error accepting connection on port {}: {}", port, e);
                            break;
                        }
                    },
                    _ = close_rx.changed() => {
                        log::debug!("Shutting down listener for port {}", port);
                        break;
                    }
                }
            }
        });

        Ok(local_addr)
    }

    /// Closes the tunnel client connection and stops all local port listeners.
    pub async fn close(self) -> Result<(), TunnelError> {
        self.close_tx.send(true).ok();
        self.session
            .disconnect(russh::Disconnect::ByApplication, "disconnect", "en")
            .await
            .map_err(TunnelError::TunnelRelayDisconnected)
    }

    /// Returns true if the underlying SSH session has been closed.
    pub fn is_closed(&self) -> bool {
        self.session.is_closed()
    }
}

/// Relays data between a local TCP stream and a tunnel channel.
async fn relay_tcp_to_channel(
    mut stream: tokio::net::TcpStream,
    session: &russh::client::Handle<TunnelClientHandler>,
    port: u16,
) -> Result<(), io::Error> {
    let channel = session
        .channel_open_direct_tcpip("127.0.0.1", port as u32, "127.0.0.1", 0)
        .await
        .map_err(|e| io::Error::other(format!("failed to open channel: {}", e)))?;
    let mut conn = PortConnection { channel };

    let mut read_buf = vec![0u8; 1024 * 64].into_boxed_slice();
    loop {
        tokio::select! {
            n = stream.read(&mut read_buf) => match n {
                Ok(0) => {
                    log::debug!("EOF from local TCP stream on port {}", port);
                    conn.close().await;
                    break;
                }
                Ok(n) => {
                    if conn.send(&read_buf[..n]).await.is_err() {
                        log::debug!("channel closed while writing on port {}", port);
                        break;
                    }
                }
                Err(e) => {
                    log::debug!("error reading from local TCP on port {}: {}", port, e);
                    conn.close().await;
                    break;
                }
            },
            data = conn.recv() => match data {
                Some(data) => {
                    if let Err(e) = stream.write_all(&data).await {
                        log::debug!("error writing to local TCP on port {}: {}", port, e);
                        break;
                    }
                }
                None => {
                    log::debug!("channel closed on port {}", port);
                    break;
                }
            },
        }
    }

    Ok(())
}

/// A connection to a forwarded port on the tunnel host. This is the
/// client-side equivalent of `ForwardedPortConnection`.
pub struct PortConnection {
    channel: russh::Channel<russh::client::Msg>,
}

impl PortConnection {
    /// Sends data on the connection.
    pub async fn send(&mut self, d: &[u8]) -> Result<(), ()> {
        self.channel.data(d).await.map_err(|_| ())
    }

    /// Receives data from the connection, returning None when it's closed.
    pub async fn recv(&mut self) -> Option<Vec<u8>> {
        loop {
            match self.channel.wait().await {
                Some(ChannelMsg::Data { data }) => return Some(data.to_vec()),
                Some(ChannelMsg::Eof | ChannelMsg::Close) => return None,
                None => return None,
                _ => {} // skip other message types
            }
        }
    }

    /// Closes the connection.
    pub async fn close(mut self) {
        self.channel.eof().await.ok();
        self.channel.close().await.ok();
    }

    /// Returns an AsyncRead/AsyncWrite implementation for the connection.
    pub fn into_rw(self) -> PortConnectionRW {
        PortConnectionRW(self.channel.into_stream())
    }
}

/// AsyncRead + AsyncWrite wrapper for a client port connection.
/// This is the client-side equivalent of `ForwardedPortRW`.
pub struct PortConnectionRW(russh::ChannelStream);

impl AsyncRead for PortConnectionRW {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        Pin::new(&mut self.0).poll_read(cx, buf)
    }
}

impl AsyncWrite for PortConnectionRW {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &[u8],
    ) -> Poll<Result<usize, io::Error>> {
        Pin::new(&mut self.0).poll_write(cx, buf)
    }
    fn poll_flush(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Pin::new(&mut self.0).poll_flush(cx)
    }
    fn poll_shutdown(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Pin::new(&mut self.0).poll_shutdown(cx)
    }
}

/// SSH client handler for the tunnel client connection.
struct TunnelClientHandler;

#[async_trait]
impl russh::client::Handler for TunnelClientHandler {
    type Error = russh::Error;

    async fn check_server_key(
        self,
        _server_public_key: &russh_keys::key::PublicKey,
    ) -> Result<(Self, bool), Self::Error> {
        // The relay authenticates via the access token; we don't need to
        // verify the host key.
        Ok((self, true))
    }
}
