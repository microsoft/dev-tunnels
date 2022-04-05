package com.microsoft.tunnels.websocket;

import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;
import io.netty.handler.codec.http.websocketx.BinaryWebSocketFrame;

/**
 * Converts the WebSocket frame to a ByteBuffer for the ssh session.
 */
public class WebSocketInboundAdapter extends ChannelInboundHandlerAdapter {
  private WebSocketSession session;

  public WebSocketInboundAdapter(WebSocketSession session) {
    super();
    this.session = session;
  }

  @Override
  public void channelActive(ChannelHandlerContext ctx) throws Exception {
    session.channelActive(ctx);
  }

  @Override
  public void channelInactive(ChannelHandlerContext ctx) throws Exception {
    session.channelInactive(ctx);
  }

  @Override
  public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
    BinaryWebSocketFrame frame = (BinaryWebSocketFrame) msg;
    session.channelRead(ctx, frame.content());
  }

  @Override
  public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
    session.exceptionCaught(ctx, cause);
  }
}
