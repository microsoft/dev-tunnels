package com.microsoft.tunnels.websocket;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelPromise;
import io.netty.handler.codec.http.websocketx.BinaryWebSocketFrame;

import java.net.SocketAddress;
import java.net.URI;

import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoWriteFuture;
import org.apache.sshd.common.util.buffer.Buffer;
import org.apache.sshd.netty.NettyIoService;
import org.apache.sshd.netty.NettyIoSession;

public class WebSocketSession extends NettyIoSession {
  protected WebSocketConnectionHandler webSocketConnectionHandler;
  protected WebSocketInboundAdapter webSocketInboundAdapter;

  /**
   * Creates a modified Netty session to handle websocket messages.
   */
  public WebSocketSession(
      NettyIoService service,
      IoHandler handler,
      SocketAddress acceptanceAddress,
      URI webSocketUri,
      String accessToken) {
    super(service, handler, acceptanceAddress);
    this.webSocketConnectionHandler = new WebSocketConnectionHandler(
        this,
        webSocketUri,
        accessToken);
    this.webSocketInboundAdapter = new WebSocketInboundAdapter(this);
  }

  @Override
  public IoWriteFuture writeBuffer(Buffer buffer) {
    int bufLen = buffer.available();
    ByteBuf buf = Unpooled.buffer(bufLen);
    buf.writeBytes(buffer.array(), buffer.rpos(), bufLen);
    DefaultIoWriteFuture msg = new DefaultIoWriteFuture(getRemoteAddress(), null);
    ChannelPromise next = context.newPromise();
    prev.addListener(whatever -> {
      if (context != null) {
        context.writeAndFlush(new BinaryWebSocketFrame(buf), next);
      }
    });
    prev = next;
    next.addListener(fut -> {
      if (fut.isSuccess()) {
        msg.setValue(Boolean.TRUE);
      } else {
        msg.setValue(fut.cause());
      }
    });
    return msg;
  }

  @Override
  public void channelActive(ChannelHandlerContext ctx) throws Exception {
    super.channelActive(ctx);
  }

  @Override
  public void channelInactive(ChannelHandlerContext ctx) throws Exception {
    super.channelInactive(ctx);
  }

  @Override
  public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
    super.channelRead(ctx, msg);
  }

  @Override
  public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
    super.exceptionCaught(ctx, cause);
  }
}
