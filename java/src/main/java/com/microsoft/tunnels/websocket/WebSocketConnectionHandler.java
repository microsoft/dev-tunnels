package com.microsoft.tunnels.websocket;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelPromise;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.handler.codec.http.DefaultHttpHeaders;
import io.netty.handler.codec.http.FullHttpResponse;
import io.netty.handler.codec.http.HttpHeaders;
import io.netty.handler.codec.http.websocketx.BinaryWebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshaker;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshakerFactory;
import io.netty.handler.codec.http.websocketx.WebSocketHandshakeException;
import io.netty.handler.codec.http.websocketx.WebSocketVersion;

import java.net.URI;

public class WebSocketConnectionHandler extends SimpleChannelInboundHandler<Object> {
  private final String subprotocol = "tunnel-relay-client";
  private final WebSocketClientHandshaker handshaker;
  private ChannelPromise handshakeFuture;
  private WebSocketSession session;

  /**
   * Handles the initial websocket upgrade and converts subsequent websocket
   * frames to byte buffers for the ssh session.
   */
  public WebSocketConnectionHandler(
      WebSocketSession session,
      URI webSocketUri,
      String accessToken) {
    super();
    HttpHeaders headers = new DefaultHttpHeaders();
    headers.set("Authorization", "tunnel " + accessToken);
    this.handshaker = WebSocketClientHandshakerFactory.newHandshaker(
        webSocketUri,
        WebSocketVersion.V13,
        subprotocol,
        true,
        headers);
    this.session = session;
  }

  @Override
  public void channelActive(ChannelHandlerContext ctx) throws Exception {
    handshaker.handshake(ctx.channel());
  }

  @Override
  public void channelInactive(ChannelHandlerContext ctx) throws Exception {
    session.channelInactive(ctx);
  }

  @Override
  public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
    if (!handshaker.isHandshakeComplete()) {
      this.channelRead0(ctx, msg);
    } else if (msg instanceof BinaryWebSocketFrame) {
      BinaryWebSocketFrame frame = (BinaryWebSocketFrame) msg;
      session.channelRead(ctx, frame.content());
    }
  }

  @Override
  public void channelRead0(ChannelHandlerContext ctx, Object msg) throws Exception {
    Channel ch = ctx.channel();
    if (!handshaker.isHandshakeComplete()) {
      try {
        handshaker.finishHandshake(ch, (FullHttpResponse) msg);
        handshakeFuture.setSuccess();
        session.channelActive(ctx);
      } catch (WebSocketHandshakeException e) {
        handshakeFuture.setFailure(e);
      }
      return;
    }
  }

  public ChannelFuture handshakeFuture() {
    return handshakeFuture;
  }

  @Override
  public void handlerAdded(ChannelHandlerContext ctx) {
    handshakeFuture = ctx.newPromise();
  }

  @Override
  public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
    session.exceptionCaught(ctx, cause);
  }
}
