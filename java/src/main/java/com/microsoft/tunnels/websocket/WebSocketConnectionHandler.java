// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.websocket;

import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;
import io.netty.channel.ChannelPromise;
import io.netty.handler.codec.http.DefaultHttpHeaders;
import io.netty.handler.codec.http.FullHttpResponse;
import io.netty.handler.codec.http.HttpHeaders;
import io.netty.handler.codec.http.HttpMessage;
import io.netty.handler.codec.http.websocketx.BinaryWebSocketFrame;
import io.netty.handler.codec.http.websocketx.PongWebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshaker;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshakerFactory;
import io.netty.handler.codec.http.websocketx.WebSocketHandshakeException;
import io.netty.handler.codec.http.websocketx.WebSocketVersion;

import java.net.URI;

public class WebSocketConnectionHandler extends ChannelInboundHandlerAdapter {
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

  /**
   * Calling channelActive on the session itself is deferred until the websocket
   * handshake is complete in the initial channelRead
   */
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
    if (msg instanceof HttpMessage) {
      if (!handshaker.isHandshakeComplete()) {
        try {
          handshaker.finishHandshake(ctx.channel(), (FullHttpResponse) msg);
          handshakeFuture.setSuccess();
          session.channelActive(ctx);
        } catch (WebSocketHandshakeException e) {
          handshakeFuture.setFailure(e);
        }
        return;
      } else {
        throw new Error("Unexpected HTTP Message: " + msg.toString());
      }
    } else if (msg instanceof BinaryWebSocketFrame) {
      BinaryWebSocketFrame frame = (BinaryWebSocketFrame) msg;
      session.channelRead(ctx, frame.content());
      return;
    } else if (msg instanceof PongWebSocketFrame) {
      // ignore keep alive message response.
      return;
    } else {
      throw new Error("Unexpected message: " + msg.toString() + " of type: " + msg.getClass().getName());
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
