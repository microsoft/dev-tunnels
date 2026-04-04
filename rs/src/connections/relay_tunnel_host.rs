// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::{
    collections::HashMap,
    env, io,
    net::{IpAddr, Ipv4Addr, Ipv6Addr, SocketAddr},
    pin::Pin,
    sync::Arc,
    task::Poll,
    time::Duration,
};

use crate::{
    contracts::{TunnelConnectionMode, TunnelEndpoint, TunnelPort, TunnelRelayTunnelEndpoint},
    management::{
        Authorization, HttpError, TunnelLocator, TunnelManagementClient, TunnelRequestOptions,
        NO_REQUEST_OPTIONS,
    },
};
use async_trait::async_trait;
use futures::{future::BoxFuture, stream::FuturesUnordered, StreamExt, TryFutureExt};
use russh::{server::Server as ServerTrait, CryptoVec};
use tokio::{
    io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt},
    net::TcpStream,
    sync::{mpsc, oneshot, watch},
    task::JoinHandle,
};
use tokio_tungstenite::{MaybeTlsStream, WebSocketStream};
use uuid::Uuid;

use super::{
    errors::TunnelError,
    ws::{build_websocket_request, connect_directly, connect_via_proxy, AsyncRWWebSocket},
};

/// Mapping of port numbers to senders to which new port connections should be
/// sent. Shared by the host relay to each connected session.
type PortMap = HashMap<u32, mpsc::UnboundedSender<ForwardedPortConnection>>;

// @group Reconnection : Types for automatic reconnection with exponential backoff

/// The connection state of a persistent relay host.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RelayConnectionState {
    /// Actively connected to the relay.
    Connected,
    /// Connection was lost; waiting before the next reconnect attempt.
    Reconnecting {
        /// 1-based attempt counter.
        attempt: u32,
        /// Milliseconds until the next connection attempt.
        delay_ms: u64,
    },
    /// Permanently disconnected (clean shutdown or max retries exceeded).
    Disconnected,
}

/// Observable state of the SSH keep-alive probing for a [`PersistentRelayHandle`].
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum KeepAliveState {
    /// Keep-alive is not configured (default).
    NotConfigured,
    /// The most recent keep-alive probe succeeded.
    Succeeded {
        /// Number of successful probes so far.
        count: u32,
    },
    /// The most recent keep-alive probe failed or timed out.
    Failed {
        /// Number of failed probes so far.
        count: u32,
    },
}

/// Controls the back-off behaviour of [`RelayTunnelHost::connect_persistent`].
pub struct ReconnectOptions {
    /// Maximum number of reconnect attempts before giving up.
    /// `None` (default) retries indefinitely.
    pub max_attempts: Option<u32>,
    /// Delay before the first retry, in milliseconds. Default: 1 000 ms.
    pub initial_delay_ms: u64,
    /// Upper bound on retry delay, in milliseconds. Default: 13 000 ms.
    pub max_delay_ms: u64,
    /// Interval between SSH keep-alive probes. `None` (default) disables keep-alive
    /// (WebSocket-level pings still run regardless).
    pub keep_alive_interval: Option<Duration>,
    /// Async callback invoked when the access token is rejected (HTTP 401).
    /// Should return a fresh token, or `None` if a new token cannot be obtained.
    /// When `None` (default), unauthorized errors follow normal back-off.
    pub token_refresher: Option<Arc<dyn Fn() -> BoxFuture<'static, Option<String>> + Send + Sync>>,
}

impl Default for ReconnectOptions {
    fn default() -> Self {
        Self {
            max_attempts: None,
            initial_delay_ms: 1_000,
            max_delay_ms: 13_000,
            keep_alive_interval: None,
            token_refresher: None,
        }
    }
}

/// Handle returned by [`RelayTunnelHost::connect_persistent`].
///
/// Drop this value (or call [`PersistentRelayHandle::stop`]) to request a
/// clean shutdown of the reconnect loop.
pub struct PersistentRelayHandle {
    /// Observe connection-state changes as they happen.
    pub state: watch::Receiver<RelayConnectionState>,
    /// Observe keep-alive probe state changes as they happen.
    pub keep_alive: watch::Receiver<KeepAliveState>,
    /// Dropping this sender signals the reconnect loop to exit.
    _stop_tx: mpsc::Sender<()>,
    join: JoinHandle<Result<(), TunnelError>>,
}

impl PersistentRelayHandle {
    /// Signals the reconnect loop to stop and waits for a clean exit.
    pub async fn stop(self) -> Result<(), TunnelError> {
        drop(self._stop_tx);
        self.join.await.unwrap_or(Ok(()))
    }
}

/// The RelayTunnelHost can host connections via the tunneling service. After
/// creating it, you will generally want to run `connect()` to create a new
/// a new connection.
///
/// Note that, while ports can be added and remove dynamically from running
/// tunnels via the appropriate methods on the RelayTunnelHost, no ports will be
/// hosted until those methods are called.
pub struct RelayTunnelHost {
    pub proxy: Option<String>,
    locator: TunnelLocator,
    host_id: Uuid,
    ports_tx: watch::Sender<PortMap>,
    ports_rx: watch::Receiver<PortMap>,
    mgmt: TunnelManagementClient,
    host_keypair: russh_keys::key::KeyPair,
}

/// Hello friend. You're probably here because you want to change how tunnel
/// connections work! Here's how it works.
///
/// ## Overall Communication
///
/// Tunneling communicates via SSH over websockets. Data is sent fairly opaquely in
/// binary websocket messages. We use Tungstenite for the websocket, and then
/// wrap it into an AsyncRead/AsyncWrite type that we can give to the websocket
/// library, russh.
///
/// We then connect over SSH. Today, Tunneling doesn't require additional auth in
/// the SSH connection, and also has a somewhat non-standard "none" value for
/// its key exchange. This prevents many off-the-shelf SSH implementations from
/// working. Once a client connects to the other end of the tunnel, it'll
/// open a new Channel from the server (of type "client-ssh-session-stream").
///
/// The stream of data over this channel is actually an SSH client connection.
/// So, for each client, we create an SSH server instance, and do some more
/// work to make that channel AsyncRead/AsyncWrite as well. (Note that this
/// client actually does do a key exchange and it data is encrypted.) Once
/// established, the host (this program) requests the client to forward the
/// ports that it via `tcpip-forward` requests. Clients then open
/// `forwarded-tcpip` channels for each new connection.
///
/// ```text
///        ┌───────────┐     ┌───────┐      ┌───────┐
///        │Host (this)│     │Service│      │Client │
///        └─────┬─────┘     └───┬───┘      └───┬───┘
///              │ Connect as    │              │
///              ├─SSH client────▶              │
///              │               │  Connect to  │
///              │               ◀──service ws──┤
///              │   Create new  │              │
///              ◀───SSH tunnel──┤              │
///              │               │              │
///              │    SSH server handshake.     │
///              ├────(Service just proxies ────▶
///              │      traffic through)        │
///              │               │              │
///              ├────tcpip-forward for ports───▶
///              │               │              │
///              │               │              ◀───asked to
///              │               │              │   connect
///              ◀────create forwarded-tcpip ───┤
///      make    │            channel           │
/// local tcp ◀──┤               │              │
/// connection   │               │              │
///              ◀ ─ ─ ─ ─forward traffic─ ─ ─ ─▶
///              │               │              │
///              │               │              │
///              ▼               ▼              ▼
/// ```
///
/// ## How this Package Works
///
/// The RelayTunnelHost allows the consumer to `connect()` to a tunnel. It's legal
/// to call this in parallel (though not generally useful...) The host keeps
/// a map of the forwarded ports in a tokio watch channel, which allows
/// connected channels to update them in realtime. Each port also has a channel
/// on which incoming connections can be received.
///
/// When the client creates a new channel for a client, that's sent back on
/// a channel, where it's wrapepd in the "AsyncRWChannel" to provide
/// AsyncRead/Write traits, and then spawned in its own Tokio task.
///
/// It watches the ports list, and when new forwarded channels are made, it
/// wraps those in a ForwardedPortConnection struct, and sends those to the
/// channel on the port record.
///
/// Ports are handled by a client calling `add_port()` or `add_port_raw()`,
/// which either forward to a local TCP connection or return the
/// ForwardedPortConnection directly, respectively.

#[allow(dead_code)]
impl RelayTunnelHost {
    pub fn new(locator: TunnelLocator, mgmt: TunnelManagementClient) -> Self {
        let host_id = Uuid::new_v4();
        let (ports_tx, ports_rx) = watch::channel(HashMap::new());
        RelayTunnelHost {
            proxy: env::var("HTTPS_PROXY").or(env::var("https_proxy")).ok(),
            host_id,
            locator,
            ports_tx,
            ports_rx,
            mgmt,
            host_keypair: russh_keys::key::KeyPair::generate_rsa(
                2048,
                russh_keys::key::SignatureHash::SHA2_512,
            )
            .expect("expected to generate rsa keypair"),
        }
    }

    /// Creates a connection and returns a handle to the tunnel relay. When
    /// created, the tunnel will forward all ports currently on the tunnel.
    /// The returned handle is a future that completes when the tunnel closes.
    /// For example:
    ///
    /// ```ignore
    /// let handle = relay.connect("host_token").await?;
    ///
    /// tokio::spawn(async move || {
    ///     loop {
    ///       tokio::select! {
    ///         port = add_port.recv() => handle.add_port(port).await?;
    ///         handle => break,
    ///       }
    ///     }
    /// });
    /// ```
    ///
    /// The handle may be dropped in order to disconnect from the relay, and
    /// will be closed if connection to the relay fails. Consumers should
    /// reconnect if this happens, and they can reconnect using the same
    /// RelayTunnelHost.
    pub async fn connect(&mut self, host_token: &str) -> Result<RelayHandle, TunnelError> {
        relay_connect_once(
            &self.mgmt,
            &self.locator,
            self.host_id,
            &self.proxy,
            self.host_keypair.clone(),
            self.ports_rx.clone(),
            host_token,
            None, // keep-alive not configured for single connect
        )
        .await
    }

    /// Connects to the relay and automatically reconnects on disconnection.
    ///
    /// Unlike [`connect`], this method retries indefinitely (or up to
    /// `options.max_attempts` times) with exponential back-off.
    ///
    /// The first connection attempt is made eagerly so callers surface
    /// configuration errors immediately. Drop the returned
    /// [`PersistentRelayHandle`] (or call [`PersistentRelayHandle::stop`]) to
    /// request a clean shutdown.
    // @group Reconnection : Persistent connection with automatic exponential backoff
    pub async fn connect_persistent(
        &mut self,
        host_token: String,
        options: ReconnectOptions,
    ) -> Result<PersistentRelayHandle, TunnelError> {
        // Fail-fast: establish the first connection eagerly.
        let (ka_tx, ka_rx) = watch::channel(KeepAliveState::NotConfigured);
        let ka_tx_arc = Arc::new(ka_tx);

        let initial_handle = relay_connect_once(
            &self.mgmt,
            &self.locator,
            self.host_id,
            &self.proxy,
            self.host_keypair.clone(),
            self.ports_rx.clone(),
            &host_token,
            options.keep_alive_interval.map(|d| (d, ka_tx_arc.clone())),
        )
        .await?;

        let (state_tx, state_rx) = watch::channel(RelayConnectionState::Connected);
        let (stop_tx, mut stop_rx) = mpsc::channel::<()>(1);

        let mgmt = self.mgmt.clone();
        let locator = self.locator.clone();
        let host_id = self.host_id;
        let proxy = self.proxy.clone();
        let host_keypair = self.host_keypair.clone();
        let ports_rx = self.ports_rx.clone();

        let join = tokio::spawn(async move {
            let mut current_join = initial_handle.join;
            let mut delay_ms = options.initial_delay_ms;
            // @group Reconnection > Token Refresh : Track single-attempt token refresh per session
            let mut current_host_token = host_token;

            'reconnect: loop {
                // Wait for the current connection to finish or a stop signal.
                tokio::select! {
                    r = &mut current_join => {
                        match r {
                            Ok(Ok(())) => log::debug!("relay connection ended cleanly"),
                            Ok(Err(e)) => log::warn!("relay connection ended with error: {}", e),
                            Err(_) => log::warn!("relay task panicked"),
                        }
                    }
                    _ = stop_rx.recv() => {
                        current_join.abort();
                        let _ = current_join.await;
                        break 'reconnect;
                    }
                }

                // Reconnect inner loop: retry with exponential back-off.
                let mut attempt: u32 = 0;
                // @group Reconnection > SSH-level Reconnection : Skip delay after SSH protocol failures
                let mut skip_delay = false;
                // @group Reconnection > Token Refresh : Single refresh per reconnect session
                let mut token_refreshed = false;
                loop {
                    attempt += 1;
                    if let Some(max) = options.max_attempts {
                        if attempt > max {
                            let _ = state_tx.send(RelayConnectionState::Disconnected);
                            return Err(TunnelError::MaxReconnectAttemptsExceeded(max));
                        }
                    }

                    let effective_delay = if skip_delay { 0 } else { delay_ms };
                    skip_delay = false;
                    let _ = state_tx.send(RelayConnectionState::Reconnecting {
                        attempt,
                        delay_ms: effective_delay,
                    });

                    if effective_delay > 0 {
                        log::info!(
                            "waiting {}ms before reconnect attempt {}",
                            effective_delay, attempt
                        );
                        tokio::select! {
                            _ = tokio::time::sleep(Duration::from_millis(effective_delay)) => {}
                            _ = stop_rx.recv() => { break 'reconnect; }
                        }
                    }

                    delay_ms = (delay_ms * 2).min(options.max_delay_ms);

                    match relay_connect_once(
                        &mgmt,
                        &locator,
                        host_id,
                        &proxy,
                        host_keypair.clone(),
                        ports_rx.clone(),
                        &current_host_token,
                        options.keep_alive_interval.map(|d| (d, ka_tx_arc.clone())),
                    )
                    .await
                    {
                        Ok(handle) => {
                            log::info!("reconnected to relay on attempt {}", attempt);
                            let _ = state_tx.send(RelayConnectionState::Connected);
                            current_join = handle.join;
                            delay_ms = options.initial_delay_ms;
                            break; // exit inner loop, wait for new connection
                        }
                        // @group Reconnection > SSH-level Reconnection : SSH error, retry once immediately
                        Err(TunnelError::TunnelRelayDisconnected(_)) => {
                            log::warn!(
                                "SSH-level failure on attempt {}, retrying immediately",
                                attempt
                            );
                            delay_ms = options.initial_delay_ms;
                            skip_delay = true;
                        }
                        // @group Reconnection > Token Refresh : HTTP 401, call token_refresher
                        Err(TunnelError::HttpError {
                            error: HttpError::ResponseError(ref resp_err),
                            ..
                        }) if resp_err.status_code == reqwest::StatusCode::UNAUTHORIZED => {
                            if let Some(refresher) = &options.token_refresher {
                                if !token_refreshed {
                                    log::info!(
                                        "access token rejected (HTTP 401), refreshing"
                                    );
                                    match refresher().await {
                                        Some(new_token) => {
                                            current_host_token = new_token;
                                            token_refreshed = true;
                                            skip_delay = true;
                                        }
                                        None => {
                                            log::warn!("token refresher returned None");
                                            let _ = state_tx.send(
                                                RelayConnectionState::Disconnected,
                                            );
                                            return Err(TunnelError::TokenRefreshFailed);
                                        }
                                    }
                                } else {
                                    log::warn!("still unauthorized after token refresh");
                                    let _ = state_tx.send(
                                        RelayConnectionState::Disconnected,
                                    );
                                    return Err(TunnelError::TokenRefreshFailed);
                                }
                            } else {
                                log::warn!(
                                    "reconnect attempt {} failed: unauthorized (no token refresher)",
                                    attempt
                                );
                            }
                        }
                        Err(e) => {
                            log::warn!("reconnect attempt {} failed: {}", attempt, e);
                            // loop continues with next attempt
                        }
                    }
                }
            }

            let _ = state_tx.send(RelayConnectionState::Disconnected);
            Ok(())
        });

        Ok(PersistentRelayHandle {
            state: state_rx,
            keep_alive: ka_rx,
            _stop_tx: stop_tx,
            join,
        })
    }

    /// Unregisters relay from the tunnel's list of hosts.
    pub async fn unregister(&self) -> Result<bool, TunnelError> {
        self.mgmt
            .delete_tunnel_endpoints(
                &self.locator,
                &format!("{}-relay", &self.host_id.to_string()),
                NO_REQUEST_OPTIONS,
            )
            .await
            .map_err(|e| TunnelError::HttpError {
                error: e,
                reason: "could not unregister relay",
            })
    }

    /// Adds a new port to the relay and returns a receiver for connections
    /// that are made to that port. This is a "low level" type that you can use
    /// if you want to deal with forwarding manually, but the `add_port` method
    /// is appropriate and simpler for most use cases.
    ///
    /// Calling this method multiple times with the same port will result in
    /// an error. Dropping the receiver **will not** remove the port, you must
    /// call `remove_port()` to do that.
    pub async fn add_port_raw(
        &self,
        port_to_add: &TunnelPort,
    ) -> Result<mpsc::UnboundedReceiver<ForwardedPortConnection>, TunnelError> {
        let n = port_to_add.port_number as u32;
        if self.ports_tx.borrow().get(&n).is_some() {
            return Err(TunnelError::PortAlreadyExists(n));
        }

        let tunnel_port = self
            .mgmt
            .create_tunnel_port(&self.locator, port_to_add, NO_REQUEST_OPTIONS)
            .await;

        match tunnel_port {
            // created the port:
            Ok(_) => {}
            // the port's already registered, nothing we need to do:
            Err(HttpError::ResponseError(e)) if e.status_code == 409 => {}
            Err(e) => {
                return Err(TunnelError::HttpError {
                    error: e,
                    reason: "failed to add port to tunnel",
                })
            }
        }

        let (tx, rx) = mpsc::unbounded_channel();
        self.ports_tx.send_modify(|v| {
            v.insert(n, tx);
        });

        Ok(rx)
    }

    /// Adds a new port to the tunnel and forwards TCP/IP connections made
    /// over that port to the local machine. Calling this method multiple times
    /// with the same port will result in an error.
    pub async fn add_port(&self, port_to_add: &TunnelPort) -> Result<(), TunnelError> {
        let rx = self.add_port_raw(port_to_add).await?;
        tokio::spawn(forward_port_to_tcp(port_to_add.port_number, rx));
        Ok(())
    }

    /// Removes a port from the tunnel connection. Any channel returned from
    /// `add_port_raw`, and any connections made within `add_port`, will close
    /// shortly after this is called.
    pub async fn remove_port(&self, port_number: u16) -> Result<(), TunnelError> {
        self.mgmt
            .delete_tunnel_port(&self.locator, port_number, NO_REQUEST_OPTIONS)
            .await
            .map_err(|e| TunnelError::HttpError {
                error: e,
                reason: "failed to remove port from tunnel",
            })?;

        self.ports_tx.send_modify(|v| {
            v.remove(&(port_number as u32));
        });

        Ok(())
    }

    fn make_ssh_server(keypair: russh_keys::key::KeyPair) -> Server {
        let c = russh::server::Config {
            connection_timeout: None,
            auth_rejection_time: std::time::Duration::from_secs(5),
            keys: vec![keypair],
            window_size: 1024 * 1024,
            preferred: russh::Preferred::COMPRESSED,
            limits: russh::Limits {
                rekey_read_limit: usize::MAX,
                rekey_time_limit: Duration::MAX,
                rekey_write_limit: usize::MAX,
            },
            ..Default::default()
        };

        let config = Arc::new(c);
        Server { config }
    }

    async fn make_ssh_client(
        rw: impl AsyncRead + AsyncWrite + Unpin + Send + 'static,
    ) -> Result<
        (
            russh::client::Handle<Client>,
            mpsc::UnboundedReceiver<ChannelOp>,
        ),
        russh::Error,
    > {
        let config = russh::client::Config {
            anonymous: true,
            window_size: 1024 * 1024 * 5,
            preferred: russh::Preferred {
                kex: &[russh::kex::NONE],
                key: &[russh_keys::key::NONE],
                cipher: &[russh::cipher::NONE],
                mac: russh::Preferred::DEFAULT.mac,
                compression: &["none"],
            },
            limits: russh::Limits {
                rekey_read_limit: 1024 * 1024 * 8,
                rekey_time_limit: std::time::Duration::from_secs(60),
                rekey_write_limit: 1024 * 1024 * 8,
            },
            ..Default::default()
        };

        let config = Arc::new(config);
        let (client, rx) = Client::new();
        let session = russh::client::connect_stream(config, rw, client).await?;

        Ok((session, rx))
    }
}

// @group Reconnection : Free helper functions backing connect() and connect_persistent()

async fn create_relay_websocket(
    mgmt: &TunnelManagementClient,
    locator: &TunnelLocator,
    host_id: Uuid,
    proxy: &Option<String>,
    host_token: &str,
) -> Result<
    (
        WebSocketStream<MaybeTlsStream<TcpStream>>,
        TunnelRelayTunnelEndpoint,
    ),
    TunnelError,
> {
    let endpoint = mgmt
        .update_tunnel_relay_endpoints(
            locator,
            &TunnelRelayTunnelEndpoint {
                base: TunnelEndpoint {
                    id: Some(format!("{}-relay", host_id)),
                    connection_mode: TunnelConnectionMode::TunnelRelay,
                    host_id: host_id.to_string(),
                    host_public_keys: vec![],
                    port_uri_format: None,
                    port_ssh_command_format: None,
                    ssh_gateway_public_key: None,
                    tunnel_ssh_command: None,
                    tunnel_uri: None,
                },
                client_relay_uri: None,
                host_relay_uri: None,
            },
            &TunnelRequestOptions {
                authorization: Some(Authorization::Tunnel(host_token.to_string())),
                ..TunnelRequestOptions::default()
            },
        )
        .await
        .map_err(|e| TunnelError::HttpError {
            error: e,
            reason: "failed to update tunnel endpoint for hosting",
        })?;

    let url = endpoint
        .host_relay_uri
        .as_deref()
        .ok_or(TunnelError::MissingHostEndpoint)?;

    let req = build_websocket_request(
        url,
        &[
            ("Sec-WebSocket-Protocol", "tunnel-relay-host"),
            ("Authorization", &format!("tunnel {}", host_token)),
            ("User-Agent", mgmt.user_agent.to_str().unwrap()),
        ],
    )?;

    let cnx = if let Some(proxy) = proxy {
        log::debug!("connecting via http_proxy on {}", proxy);
        connect_via_proxy(req, proxy).await?
    } else {
        connect_directly(req).await?
    };

    Ok((cnx, endpoint))
}

async fn relay_connect_once(
    mgmt: &TunnelManagementClient,
    locator: &TunnelLocator,
    host_id: Uuid,
    proxy: &Option<String>,
    host_keypair: russh_keys::key::KeyPair,
    ports_rx: watch::Receiver<PortMap>,
    host_token: &str,
    keep_alive: Option<(Duration, Arc<watch::Sender<KeepAliveState>>)>,
) -> Result<RelayHandle, TunnelError> {
    let (cnx, endpoint) =
        create_relay_websocket(mgmt, locator, host_id, proxy, host_token).await?;
    let cnx = AsyncRWWebSocket::new(super::ws::AsyncRWWebSocketOptions {
        websocket: cnx,
        ping_interval: keep_alive.as_ref().map(|(d, _)| *d).unwrap_or(Duration::from_secs(60)),
        ping_timeout: Duration::from_secs(10),
    });

    let (client_session, mut rx) = RelayTunnelHost::make_ssh_client(cnx)
        .await
        .map_err(TunnelError::TunnelRelayDisconnected)?;
    let client_session = Arc::new(client_session);
    let client_session_ret = client_session.clone();

    // @group SSH Keep-alive : Periodic liveness probe via is_closed()
    if let Some((interval, ka_tx)) = keep_alive {
        let ka_tx = ka_tx.clone();
        let session_check = client_session_ret.clone();
        tokio::spawn(async move {
            let mut count: u32 = 0;
            loop {
                tokio::time::sleep(interval).await;
                count = count.saturating_add(1);
                if session_check.is_closed() {
                    let _ = ka_tx.send(KeepAliveState::Failed { count });
                    break;
                } else {
                    let _ = ka_tx.send(KeepAliveState::Succeeded { count });
                }
            }
        });
    }


    log::debug!("established host relay primary session");

    let mut channels = HashMap::new();
    let join = tokio::spawn(async move {
        let mut server = RelayTunnelHost::make_ssh_server(host_keypair.clone());
        loop {
            tokio::select! {
                Some(op) = rx.recv() => match op {
                    ChannelOp::Open(id) => {
                        let (rw, sender) = AsyncRWChannel::new(id, client_session.clone());
                        server.run_stream(rw, ports_rx.clone());
                        channels.insert(id, sender);
                        log::info!("Opened new client on channel {}", id);
                    },
                    ChannelOp::Close(id) => {
                        channels.remove(&id);
                    },
                    ChannelOp::Data(id, data) => {
                        if let Some(ch) = channels.get(&id) {
                            if ch.send(data).is_err() {
                                channels.remove(&id);
                            }
                        }
                    },
                },
                else => break,
            }
        }

        client_session
            .disconnect(russh::Disconnect::ByApplication, "going away", "en")
            .await
            .ok();

        log::debug!("disconnected primary session after EOF");

        Ok(())
    });

    Ok(RelayHandle {
        endpoint,
        join,
        session: client_session_ret,
    })
}

/// Type returned in a channel from `add_forwarded_port_raw`, implementing
/// `AsyncRead` and `AsyncWrite`.
pub struct ForwardedPortConnection {
    port: u32,
    channel: russh::ChannelId,
    handle: russh::server::Handle,
    receiver: mpsc::Receiver<Vec<u8>>,
}

impl ForwardedPortConnection {
    /// Sends data on the connection.
    pub async fn send(&mut self, d: &[u8]) -> Result<(), ()> {
        self.handle
            .data(self.channel, CryptoVec::from_slice(d))
            .map_err(|_| ())
            .await
    }

    /// Receives data from the connection, returning None when it's closed.
    pub async fn recv(&mut self) -> Option<Vec<u8>> {
        self.receiver.recv().await
    }

    /// Closes the forwarded connection.
    pub async fn close(self) {
        self.handle.close(self.channel).await.ok();
    }

    /// Returns an AsyncRead/AsyncWrite implementation for the connection.
    pub fn into_rw(self) -> ForwardedPortRW {
        let (w, r) = self.into_split();
        ForwardedPortRW(r, w)
    }

    /// Returns a split AsyncRead/AsyncWrite half for the connection.
    pub fn into_split(self) -> (ForwardedPortWriter, ForwardedPortReader) {
        (
            ForwardedPortWriter {
                channel: self.channel,
                handle: self.handle,
                is_write_fut_valid: false,
                write_fut: tokio_util::sync::ReusableBoxFuture::new(make_server_write_fut(None)),
            },
            ForwardedPortReader {
                receiver: self.receiver,
                readbuf: super::io::ReadBuffer::default(),
            },
        )
    }
}

/// AsyncWrite implementation that can be obtained from the ForwardedPortConnection.
pub struct ForwardedPortWriter {
    channel: russh::ChannelId,
    handle: russh::server::Handle,
    is_write_fut_valid: bool,
    write_fut: tokio_util::sync::ReusableBoxFuture<'static, Result<(), russh::CryptoVec>>,
}

/// Makes a future that writes to the russh handle. This general approach was
/// taken from https://docs.rs/tokio-util/0.7.3/tokio_util/sync/struct.PollSender.html
/// This is just like make_client_write_fut, but for clients (they don't share a trait...)
async fn make_server_write_fut(
    data: Option<(russh::server::Handle, russh::ChannelId, Vec<u8>)>,
) -> Result<(), russh::CryptoVec> {
    match data {
        Some((client, id, data)) => client.data(id, CryptoVec::from(data)).await,
        None => unreachable!("this future should not be pollable in this state"),
    }
}

impl AsyncWrite for ForwardedPortWriter {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &[u8],
    ) -> Poll<Result<usize, io::Error>> {
        if !self.is_write_fut_valid {
            let handle = self.handle.clone();
            let id = self.channel;
            self.write_fut
                .set(make_server_write_fut(Some((handle, id, buf.to_vec()))));
            self.is_write_fut_valid = true;
        }

        self.poll_flush(cx).map(|r| r.map(|_| buf.len()))
    }

    fn poll_flush(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        if !self.is_write_fut_valid {
            return Poll::Ready(Ok(()));
        }

        match self.write_fut.poll(cx) {
            Poll::Pending => Poll::Pending,
            Poll::Ready(Ok(_)) => {
                self.is_write_fut_valid = false;
                Poll::Ready(Ok(()))
            }
            Poll::Ready(Err(_)) => {
                self.is_write_fut_valid = false;
                Poll::Ready(Err(io::Error::new(io::ErrorKind::Other, "EOF")))
            }
        }
    }

    fn poll_shutdown(
        self: Pin<&mut Self>,
        _cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Poll::Ready(Ok(()))
    }
}

/// AsyncRead implementation that can be obtained from the ForwardedPortConnection.
pub struct ForwardedPortReader {
    receiver: mpsc::Receiver<Vec<u8>>,
    readbuf: super::io::ReadBuffer,
}

impl AsyncRead for ForwardedPortReader {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        if let Some((v, s)) = self.readbuf.take_data() {
            return self.readbuf.put_data(buf, v, s);
        }

        match self.receiver.poll_recv(cx) {
            Poll::Ready(Some(msg)) => self.readbuf.put_data(buf, msg, 0),
            Poll::Ready(None) => Poll::Ready(Err(io::Error::new(io::ErrorKind::Other, "EOF"))),
            Poll::Pending => Poll::Pending,
        }
    }
}

pub struct ForwardedPortRW(ForwardedPortReader, ForwardedPortWriter);

impl AsyncRead for ForwardedPortRW {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        Pin::new(&mut self.0).poll_read(cx, buf)
    }
}

impl AsyncWrite for ForwardedPortRW {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &[u8],
    ) -> Poll<Result<usize, io::Error>> {
        Pin::new(&mut self.1).poll_write(cx, buf)
    }
    fn poll_flush(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Pin::new(&mut self.1).poll_flush(cx)
    }
    fn poll_shutdown(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Pin::new(&mut self.1).poll_shutdown(cx)
    }
}

#[derive(Clone)]
struct Server {
    config: Arc<russh::server::Config>,
}

impl Server {
    pub fn run_stream(
        &mut self,
        rw: impl AsyncRead + AsyncWrite + Unpin + Send + 'static,
        mut ports: watch::Receiver<PortMap>,
    ) -> JoinHandle<Result<(), russh::Error>> {
        let mut server_session = self.new_client(None);
        let mut server_connection_rx = server_session.take_rx().expect("expected to have tx");
        let authed_tx = server_session.take_authed().expect("expected to have tx");

        let config = self.config.clone();
        tokio::spawn(async move {
            log::debug!("starting to serve host relay client session");
            let session = match russh::server::run_stream(config, rw, server_session).await {
                Ok(s) => s,
                Err(e) => {
                    log::error!("error handshaking session: {}", e);
                    return Err(e);
                }
            };

            if authed_tx.await.is_err() {
                log::debug!("connection closed before auth");
                return Ok(()); // session closed
            }

            log::debug!("host relay client session successfully authed");
            let mut known_ports: PortMap = HashMap::new();
            tokio::pin!(session);

            loop {
                tokio::select! {
                    r = &mut session => return r,
                    cnx = server_connection_rx.recv() => match cnx {
                        Some(cnx) => {
                            if let Some(p) = known_ports.get(&cnx.port) {
                                p.send(cnx).ok(); // ignore error, could have dropped in the meantime
                            }
                        },
                        None => {
                            log::debug!("no more connections on host relay client session, ending");
                            return Ok(());
                        },
                    },
                    _ = ports.changed() => {
                        let new_ports = ports.borrow().clone();
                        for port in new_ports.keys() {
                            if !known_ports.contains_key(port) {
                                session.handle().forward_tcpip("127.0.0.1".to_string(), *port).await.ok();
                            }
                        }
                        for port in known_ports.keys() {
                            if !new_ports.contains_key(port) {
                                session.handle().cancel_forward_tcpip("127.0.0.1".to_string(), *port).await.ok();
                            }
                        }

                        known_ports = new_ports;
                    },
                }
            }
        })
    }
}

/// Connects connections that are sent to the receiver to TCP services locally.
/// Runs until the receiver is closed (usually via `delete_port()`).
async fn forward_port_to_tcp(port: u16, mut rx: mpsc::UnboundedReceiver<ForwardedPortConnection>) {
    let ipv4_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::LOCALHOST), port);
    let ipv6_addr = SocketAddr::new(IpAddr::V6(Ipv6Addr::LOCALHOST), port);
    while let Some(mut conn) = rx.recv().await {
        // Try connecting to ipv4 and ipv6 in parallel, using the first successful
        // connection in the stream. A downside is that it means if different sevices
        // are listening on the ports, the forwarded application is non-deterministic.
        //
        // But that's rare, and in other cases, one interface will time out and
        // one interface will work, so this lets us respond as quickly as possible.
        let mut futs = FuturesUnordered::new();
        futs.push(TcpStream::connect(&ipv4_addr));
        futs.push(TcpStream::connect(&ipv6_addr));

        let mut last_result = None;
        while let Some(r) = futs.next().await {
            let ok = r.is_ok();
            last_result = Some(r);
            // stop on first successful:
            if ok {
                break;
            }
        }

        // unwrap is safe since we know there will be at least one result:
        let mut stream = match last_result.unwrap() {
            Ok(s) => s,
            Err(e) => {
                log::info!("Error connecting forwarding to port {}, {}", port, e);
                conn.close().await;
                continue;
            }
        };

        log::debug!("Forwarded connection to port {}", port);

        tokio::spawn(async move {
            let mut read_buf = vec![0u8; 1024 * 64].into_boxed_slice();
            loop {
                tokio::select! {
                    n = stream.read(&mut read_buf) => match n {
                        Ok(0) => {
                            log::debug!("EOF from TCP stream, ending");
                            break;
                        },
                        Ok(n) => {
                            if (conn.send(&read_buf[..n]).await).is_err() {
                                log::debug!("channel was closed, ending forwarded port");
                                break;
                            }
                        },
                        Err(e) => {
                            log::debug!("error from TCP stream, ending: {}", e);
                            break;
                        }
                    },
                    m = conn.recv() => match m {
                        Some(data) => {
                            if let Err(e) = stream.write_all(&data).await {
                                log::debug!("error writing data to channel, ending: {}", e);
                                break;
                            }
                        },
                        None => {
                            log::debug!("EOF from channel, ending");
                            break;
                        }
                    }
                }
            }
        });
    }
}

impl ServerTrait for Server {
    type Handler = ServerHandle;
    fn new_client(&mut self, _: Option<std::net::SocketAddr>) -> ServerHandle {
        ServerHandle::new()
    }
}

struct ServerHandle {
    authed_tx: Option<oneshot::Sender<()>>,
    authed_rx: Option<oneshot::Receiver<()>>,
    cnx_tx: mpsc::UnboundedSender<ForwardedPortConnection>,
    cnx_rx: Option<mpsc::UnboundedReceiver<ForwardedPortConnection>>,
    channel_senders: HashMap<russh::ChannelId, mpsc::Sender<Vec<u8>>>,
}

impl ServerHandle {
    pub fn new() -> Self {
        let (authed_tx, authed_rx) = oneshot::channel();
        let (cnx_tx, cnx_rx) = mpsc::unbounded_channel();
        Self {
            authed_rx: Some(authed_rx),
            authed_tx: Some(authed_tx),
            cnx_rx: Some(cnx_rx),
            cnx_tx,
            channel_senders: HashMap::new(),
        }
    }

    /// Takes the receiver from a newly-created handle.
    pub fn take_rx(&mut self) -> Option<mpsc::UnboundedReceiver<ForwardedPortConnection>> {
        self.cnx_rx.take()
    }

    /// Takes the receiver from a newly-created handle.
    pub fn take_authed(&mut self) -> Option<oneshot::Receiver<()>> {
        self.authed_rx.take()
    }
}

#[async_trait]
impl russh::server::Handler for ServerHandle {
    type Error = russh::Error;

    async fn auth_succeeded(
        mut self,
        session: russh::server::Session,
    ) -> Result<(Self, russh::server::Session), Self::Error> {
        if let Some(tx) = self.authed_tx.take() {
            tx.send(()).ok();
        }
        Ok((self, session))
    }

    /// Connecting clients will use "none" auth on their channels.
    async fn auth_none(self, _: &str) -> Result<(Self, russh::server::Auth), Self::Error> {
        Ok((self, russh::server::Auth::Accept))
    }

    async fn channel_open_forwarded_tcpip(
        mut self,
        channel: russh::Channel<russh::server::Msg>,
        _host_to_connect: &str,
        port_to_connect: u32,
        _originator_address: &str,
        _originator_port: u32,
        session: russh::server::Session,
    ) -> Result<(Self, bool, russh::server::Session), Self::Error> {
        let (sender, receiver) = mpsc::channel(10);
        let txd = self.cnx_tx.send(ForwardedPortConnection {
            port: port_to_connect,
            channel: channel.id(),
            handle: session.handle(),
            receiver,
        });
        if txd.is_ok() {
            self.channel_senders.insert(channel.id(), sender);
        }

        Ok((self, true, session))
    }

    async fn data(
        mut self,
        channel: russh::ChannelId,
        data: &[u8],
        session: russh::server::Session,
    ) -> Result<(Self, russh::server::Session), Self::Error> {
        let data_vec = data.to_vec();
        if let Some(sender) = self.channel_senders.get(&channel) {
            if sender.send(data_vec).await.is_err() {
                self.channel_senders.remove(&channel);
            }
        }
        Ok((self, session))
    }
}

/// Type sent from the Handler back to the processing queue. This can be a
/// channel starting or stopping, or data on a channel.
#[derive(Debug)]
enum ChannelOp {
    Open(russh::ChannelId),
    Close(russh::ChannelId),
    Data(russh::ChannelId, Vec<u8>),
}

/// The Client implements the russh handler for the main SSH session on which
/// connections will come in via channels.
struct Client {
    sender: mpsc::UnboundedSender<ChannelOp>,
}

impl Client {
    pub fn new() -> (Self, mpsc::UnboundedReceiver<ChannelOp>) {
        let (tx, rx) = mpsc::unbounded_channel();
        (Client { sender: tx }, rx)
    }
}

#[async_trait]
impl russh::client::Handler for Client {
    type Error = russh::Error;

    async fn check_server_key(
        self,
        _server_public_key: &russh_keys::key::PublicKey,
    ) -> Result<(Self, bool), Self::Error> {
        Ok((self, true))
    }

    fn server_channel_handle_unknown(
        &self,
        channel: russh::ChannelId,
        channel_type: &[u8],
    ) -> bool {
        if channel_type == b"client-ssh-session-stream" {
            self.sender.send(ChannelOp::Open(channel)).ok();
            true
        } else {
            false
        }
    }

    async fn channel_close(
        self,
        channel: russh::ChannelId,
        session: russh::client::Session,
    ) -> Result<(Self, russh::client::Session), Self::Error> {
        self.sender.send(ChannelOp::Close(channel)).ok();
        Ok((self, session))
    }

    async fn data(
        self,
        channel: russh::ChannelId,
        data: &[u8],
        session: russh::client::Session,
    ) -> Result<(Self, russh::client::Session), Self::Error> {
        self.sender
            .send(ChannelOp::Data(channel, data.to_vec()))
            .ok();
        Ok((self, session))
    }
}

/// AsyncRead/AsyncWrite for converting SSH Channels into AsyncRead/AsyncWrite.
struct AsyncRWChannel {
    id: russh::ChannelId,
    session: Arc<russh::client::Handle<Client>>,
    incoming: mpsc::UnboundedReceiver<Vec<u8>>,

    readbuf: super::io::ReadBuffer,

    is_write_fut_valid: bool,
    write_fut: tokio_util::sync::ReusableBoxFuture<'static, Result<(), russh::CryptoVec>>,
}

impl AsyncRWChannel {
    pub fn new(
        id: russh::ChannelId,
        session: Arc<russh::client::Handle<Client>>,
    ) -> (Self, mpsc::UnboundedSender<Vec<u8>>) {
        let (tx, rx) = mpsc::unbounded_channel();
        (
            AsyncRWChannel {
                id,
                session,
                incoming: rx,
                readbuf: super::io::ReadBuffer::default(),
                is_write_fut_valid: false,
                write_fut: tokio_util::sync::ReusableBoxFuture::new(make_client_write_fut(None)),
            },
            tx,
        )
    }
}

/// Makes a future that writes to the russh handle. This general approach was
/// taken from https://docs.rs/tokio-util/0.7.3/tokio_util/sync/struct.PollSender.html
/// This is just like make_server_write_fut, but for clients (they don't share a trait...)
async fn make_client_write_fut(
    data: Option<(
        Arc<russh::client::Handle<Client>>,
        russh::ChannelId,
        Vec<u8>,
    )>,
) -> Result<(), russh::CryptoVec> {
    match data {
        Some((client, id, data)) => client.data(id, CryptoVec::from(data)).await,
        None => unreachable!("this future should not be pollable in this state"),
    }
}

impl AsyncWrite for AsyncRWChannel {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &[u8],
    ) -> Poll<Result<usize, io::Error>> {
        if !self.is_write_fut_valid {
            let session = self.session.clone();
            let id = self.id;
            self.write_fut
                .set(make_client_write_fut(Some((session, id, buf.to_vec()))));
            self.is_write_fut_valid = true;
        }

        self.poll_flush(cx).map(|r| r.map(|_| buf.len()))
    }

    fn poll_flush(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        if !self.is_write_fut_valid {
            return Poll::Ready(Ok(()));
        }

        match self.write_fut.poll(cx) {
            Poll::Pending => Poll::Pending,
            Poll::Ready(Ok(_)) => {
                self.is_write_fut_valid = false;
                Poll::Ready(Ok(()))
            }
            Poll::Ready(Err(_)) => {
                self.is_write_fut_valid = false;
                Poll::Ready(Err(io::Error::new(io::ErrorKind::Other, "EOF")))
            }
        }
    }

    fn poll_shutdown(
        self: Pin<&mut Self>,
        _cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        Poll::Ready(Ok(()))
    }
}

impl AsyncRead for AsyncRWChannel {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        if let Some((v, s)) = self.readbuf.take_data() {
            return self.readbuf.put_data(buf, v, s);
        }

        match self.incoming.poll_recv(cx) {
            Poll::Ready(Some(msg)) => self.readbuf.put_data(buf, msg, 0),
            Poll::Ready(None) => Poll::Ready(Err(io::Error::new(io::ErrorKind::Other, "EOF"))),
            Poll::Pending => Poll::Pending,
        }
    }
}

pub struct RelayHandle {
    endpoint: TunnelRelayTunnelEndpoint,
    session: Arc<russh::client::Handle<Client>>,
    join: JoinHandle<Result<(), russh::Error>>,
}

impl RelayHandle {
    /// Gets the endpoint this relay is connected to.
    pub fn endpoint(&self) -> &TunnelRelayTunnelEndpoint {
        &self.endpoint
    }

    /// Closes the tunnel and waits for all associated tasks to end.
    pub async fn close(self) -> Result<(), TunnelError> {
        let result = self
            .session
            .disconnect(russh::Disconnect::ByApplication, "disconnect", "en")
            .await;
        self.join.await.ok();
        result.map_err(TunnelError::TunnelRelayDisconnected)
    }
}

impl std::future::Future for RelayHandle {
    type Output = Result<(), TunnelError>;
    fn poll(mut self: Pin<&mut Self>, cx: &mut std::task::Context<'_>) -> Poll<Self::Output> {
        match std::future::Future::poll(Pin::new(&mut self.join), cx) {
            Poll::Ready(r) => Poll::Ready(match r {
                Ok(Ok(_)) => Ok(()),
                Ok(Err(e)) => Err(TunnelError::TunnelRelayDisconnected(e)),
                Err(_) => Ok(()),
            }),
            Poll::Pending => Poll::Pending,
        }
    }
}
