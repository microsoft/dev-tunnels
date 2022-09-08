// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::{io, pin::Pin, task::Poll, time::Duration};

use futures::{Future, Sink, Stream};
use tokio::{
    io::{AsyncRead, AsyncWrite},
    time::{sleep, Instant, Sleep},
};
use tokio_tungstenite::WebSocketStream;

use super::errors::TunnelError;

/// AsyncRead/AsyncWrite wrapper for a WebSocketStream.
pub(crate) struct AsyncRWWebSocket<S> {
    websocket: WebSocketStream<S>,
    readbuf: super::io::ReadBuffer,
    ping_timer: Pin<Box<Sleep>>,
    ping_state: PingState,
    ping_interval: Duration,
    ping_timeout: Duration,
}

enum PingState {
    WillPing,
    SendingPing,
    WaitingForPong,
}

pub(crate) struct AsyncRWWebSocketOptions<S> {
    pub websocket: WebSocketStream<S>,
    pub ping_interval: Duration,
    pub ping_timeout: Duration,
}

impl<S> AsyncRWWebSocket<S>
where
    S: AsyncRead + AsyncWrite + Unpin,
{
    pub fn new(opts: AsyncRWWebSocketOptions<S>) -> Self {
        AsyncRWWebSocket {
            websocket: opts.websocket,
            readbuf: super::io::ReadBuffer::default(),
            ping_timer: Box::pin(sleep(opts.ping_interval)),
            ping_state: PingState::WillPing,
            ping_interval: opts.ping_interval,
            ping_timeout: opts.ping_timeout,
        }
    }

    fn get_ws(&mut self) -> Pin<&mut WebSocketStream<S>> {
        Pin::new(&mut self.websocket)
    }

    fn poll_send_ping(
        &mut self,
        cx: &mut std::task::Context<'_>,
    ) -> Option<Poll<std::io::Result<()>>> {
        match self.get_ws().poll_flush(cx) {
            Poll::Ready(Ok(_)) => {
                let deadline = Instant::now() + self.ping_timeout;
                self.ping_timer.as_mut().reset(deadline);
                self.ping_state = PingState::WaitingForPong;
                log::debug!("sent liveness ping");
                None
            }
            Poll::Ready(Err(e)) => Some(Poll::Ready(Err(tung_to_io_error(e)))),
            Poll::Pending => Some(Poll::Pending),
        }
    }
}

fn tung_to_io_error(e: tungstenite::Error) -> io::Error {
    match e {
        tungstenite::Error::Io(e) => e,
        _ => io::Error::new(io::ErrorKind::Other, e.to_string()),
    }
}

impl<S> AsyncWrite for AsyncRWWebSocket<S>
where
    S: AsyncRead + AsyncWrite + Unpin,
{
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &[u8],
    ) -> Poll<Result<usize, io::Error>> {
        let sm = self.get_mut();

        match sm.get_ws().poll_ready(cx) {
            Poll::Ready(Ok(())) => {
                sm.get_ws()
                    .start_send(tungstenite::Message::Binary(buf.to_vec()))
                    .map_err(tung_to_io_error)?;
                Poll::Ready(Ok(buf.len()))
            }
            Poll::Ready(Err(e)) => Poll::Ready(Err(tung_to_io_error(e))),
            Poll::Pending => Poll::Pending,
        }
    }

    fn poll_flush(
        self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        self.get_mut()
            .get_ws()
            .poll_flush(cx)
            .map_err(tung_to_io_error)
    }

    fn poll_shutdown(
        self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
    ) -> Poll<Result<(), io::Error>> {
        self.get_mut()
            .get_ws()
            .poll_close(cx)
            .map_err(tung_to_io_error)
    }
}

impl<S> AsyncRead for AsyncRWWebSocket<S>
where
    S: AsyncRead + AsyncWrite + Unpin,
{
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut std::task::Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<std::io::Result<()>> {
        if let Some((v, s)) = self.readbuf.take_data() {
            return self.readbuf.put_data(buf, v, s);
        }

        // The following blocks implement the state machine for liveness checks
        // via a websocket ping/pong. There is a "sleep" on the struct, which
        // is bumped every time we get a new message, along with a "state".
        //
        // - When sleep times out the first time (state=WillPing), we poll the
        //   websocket for readiness, and then enqueue a ping message.
        // - When sending that ping (state=SendingPing), we poll_flush the socket
        //   until that gets sent, reset the timer, and then move to WaitForPong.
        // - The next time the timer times out, if we're still state=WaitForPong
        //   state (i.e. the state was not updated in the below read loop) then
        //   we signal EOF to the caller.

        if let PingState::SendingPing = self.ping_state {
            if let Some(ret) = self.poll_send_ping(cx) {
                return ret;
            }
        } else if Pin::new(&mut self.ping_timer).poll(cx).is_ready() {
            match self.ping_state {
                PingState::WaitingForPong => {
                    log::info!("websocket pong timed out, closing");
                    return Poll::Ready(Ok(()));
                }
                PingState::WillPing => match self.get_ws().poll_ready(cx) {
                    Poll::Ready(Ok(_)) => {
                        if let Err(e) = self.get_ws().start_send(tungstenite::Message::Ping(vec![]))
                        {
                            return Poll::Ready(Err(tung_to_io_error(e)));
                        }
                        self.ping_state = PingState::SendingPing;
                        if let Some(ret) = self.poll_send_ping(cx) {
                            return ret;
                        }
                    }
                    Poll::Ready(Err(e)) => return Poll::Ready(Err(tung_to_io_error(e))),
                    Poll::Pending => return Poll::Pending,
                },
                PingState::SendingPing => unreachable!(),
            }
        }

        // That's the end of ping/pong. Now the standard read loop:
        loop {
            match self.get_ws().poll_next(cx) {
                Poll::Ready(Some(Ok(msg))) => {
                    // bump the timeout to avoid unnecessary work if messages
                    // are still flowing.
                    let deadline = Instant::now() + self.ping_interval;
                    self.ping_timer.as_mut().reset(deadline);

                    match msg {
                        tungstenite::Message::Text(text) => {
                            return self.readbuf.put_data(buf, text.into_bytes(), 0);
                        }
                        tungstenite::Message::Binary(bin) => {
                            return self.readbuf.put_data(buf, bin, 0);
                        }
                        tungstenite::Message::Close(_) => return Poll::Ready(Ok(())),
                        tungstenite::Message::Pong(_) => {
                            log::debug!("received liveness pong");
                            self.ping_state = PingState::WillPing;
                        }
                        // Note: tungstenite handles replying to pings internally,
                        // so we don't need to handle that here.
                        _ => { /* read next */ }
                    }
                }
                Poll::Ready(Some(Err(e))) => {
                    log::info!("error reading websocket: {}", e);
                    return Poll::Ready(Err(tung_to_io_error(e)));
                }
                Poll::Ready(None) => return Poll::Ready(Ok(())),
                Poll::Pending => return Poll::Pending,
            }
        }
    }
}

/// Creates a websocket request with additional headers. This is annoyingly
/// complicated. https://github.com/snapview/tungstenite-rs/issues/107
pub(crate) fn build_websocket_request(
    url: &str,
    extra_headers: &[(&str, &str)],
) -> Result<tungstenite::handshake::client::Request, TunnelError> {
    let url =
        reqwest::Url::try_from(url).map_err(|e| TunnelError::InvalidHostEndpoint(e.to_string()))?;
    let host = url
        .host()
        .ok_or_else(|| TunnelError::InvalidHostEndpoint("missing host".to_string()))?;

    let mut req = tungstenite::handshake::client::Request::builder()
        .method("GET")
        .header("Host", host.to_string())
        .header("Connection", "Upgrade")
        .header("Upgrade", "websocket")
        .header("Sec-WebSocket-Version", "13")
        .header(
            "Sec-WebSocket-Key",
            tungstenite::handshake::client::generate_key(),
        );

    for (key, value) in extra_headers {
        req = req.header(*key, *value);
    }

    req.uri(url.as_str())
        .body(())
        .map_err(|e| TunnelError::InvalidHostEndpoint(e.to_string()))
}

#[cfg(test)]
mod test {
    use std::time::Duration;

    use futures::{StreamExt, TryStreamExt};
    use rand::RngCore;
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::{TcpListener, TcpStream},
    };
    use tokio_tungstenite::connect_async;

    use super::{build_websocket_request, AsyncRWWebSocket, AsyncRWWebSocketOptions};

    #[tokio::test]
    async fn test_websocket_stream() {
        let echo_server = TcpListener::bind("127.0.0.1:0")
            .await
            .expect("expect to listen");

        let req = build_websocket_request(
            &format!("ws://{}", echo_server.local_addr().unwrap()),
            &[("User-Agent", "test client")],
        )
        .expect("expected to make req");

        tokio::spawn(async move {
            let (cnx, _) = echo_server.accept().await.expect("expect client");
            accept_echo_server_connection(cnx).await;
        });

        let input_len = 1024 * 1024;
        let mut input = Vec::with_capacity(input_len);
        for i in 0..input_len {
            input.push(i as u8);
        }

        let (cnx, _) = connect_async(req).await.expect("expected to connect");
        let (mut read, mut write) =
            tokio::io::split(AsyncRWWebSocket::new(AsyncRWWebSocketOptions {
                ping_interval: Duration::from_secs(60),
                ping_timeout: Duration::from_secs(1),
                websocket: cnx,
            }));

        let input_dup = input.clone();
        tokio::spawn(async move {
            let mut i = 0;
            while i < input_len {
                let next = std::cmp::min(
                    input_len,
                    i + (rand::thread_rng().next_u32() % 100000) as usize,
                );
                write
                    .write_all(&input_dup[i..next])
                    .await
                    .expect("expected to write");
                i = next;
            }
        });

        let mut output = Vec::new();
        output.resize(input_len, 0);
        read.read_exact(&mut output)
            .await
            .expect("expected to read");

        assert_eq!(input, output);
    }

    async fn accept_echo_server_connection(stream: TcpStream) {
        let ws_stream = tokio_tungstenite::accept_async(stream)
            .await
            .expect("Error during the websocket handshake occurred");

        let (write, read) = ws_stream.split();
        // We should not forward messages other than text or binary.
        read.try_filter(|msg| futures::future::ready(msg.is_text() || msg.is_binary()))
            .forward(write)
            .await
            .ok();
    }
}
