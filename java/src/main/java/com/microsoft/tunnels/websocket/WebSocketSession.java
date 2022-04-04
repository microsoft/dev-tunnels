package com.microsoft.tunnels.websocket;

import java.io.IOException;
import java.lang.reflect.Method;
import java.net.Socket;
import java.net.SocketAddress;
import java.net.URI;
import java.nio.channels.SelectableChannel;
import java.nio.channels.SocketChannel;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.stream.Stream;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelFutureListener;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;
import io.netty.channel.ChannelPromise;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.channel.nio.AbstractNioChannel;
import io.netty.handler.codec.http.DefaultHttpHeaders;
import io.netty.handler.codec.http.FullHttpResponse;
import io.netty.handler.codec.http.HttpHeaders;
import io.netty.handler.codec.http.websocketx.BinaryWebSocketFrame;
import io.netty.handler.codec.http.websocketx.CloseWebSocketFrame;
import io.netty.handler.codec.http.websocketx.PongWebSocketFrame;
import io.netty.handler.codec.http.websocketx.TextWebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshaker;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshakerFactory;
import io.netty.handler.codec.http.websocketx.WebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketHandshakeException;
import io.netty.handler.codec.http.websocketx.WebSocketVersion;
import io.netty.util.Attribute;
import io.netty.util.CharsetUtil;

import org.apache.sshd.common.future.CloseFuture;
import org.apache.sshd.common.io.AbstractIoWriteFuture;
import org.apache.sshd.common.io.IoConnectFuture;
import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoService;
import org.apache.sshd.common.io.IoSession;
import org.apache.sshd.common.io.IoWriteFuture;
import org.apache.sshd.common.util.ExceptionUtils;
import org.apache.sshd.common.util.GenericUtils;
import org.apache.sshd.common.util.buffer.Buffer;
import org.apache.sshd.common.util.closeable.AbstractCloseable;
import org.apache.sshd.netty.NettySupport;

public class WebSocketSession extends AbstractCloseable implements IoSession {
  public static final Method NIO_JAVA_CHANNEL_METHOD = Stream.of(AbstractNioChannel.class.getDeclaredMethods())
      .filter(m -> "javaChannel".equals(m.getName()) && (m.getParameterCount() == 0))
      .map(m -> {
        m.setAccessible(true);
        return m;
      }).findFirst()
      .orElse(null);

  protected final Map<Object, Object> attributes = new HashMap<>();
  protected final WebSocketService service;
  protected final IoHandler handler;
  protected final long id;
  protected ChannelHandlerContext context;
  protected SocketAddress remoteAddr;
  protected ChannelFuture prev;
  protected Adapter adapter;
  protected InboundAdapter inboundAdapter;

  private final SocketAddress acceptanceAddress;

  public WebSocketSession(WebSocketService service, IoHandler handler, SocketAddress acceptanceAddress,
      URI webSocketUri, String accessToken) {
    super(Objects.toString(acceptanceAddress, ""));

    this.service = service;
    this.handler = handler;
    this.id = service.sessionSeq.incrementAndGet();
    this.acceptanceAddress = acceptanceAddress;
    this.adapter = new Adapter(webSocketUri, accessToken);
    this.inboundAdapter = new InboundAdapter();
  }

  @Override
  public long getId() {
    return id;
  }

  @Override
  public Object getAttribute(Object key) {
    synchronized (attributes) {
      return attributes.get(key);
    }
  }

  @Override
  public Object setAttribute(Object key, Object value) {
    synchronized (attributes) {
      return attributes.put(key, value);
    }
  }

  @Override
  public Object setAttributeIfAbsent(Object key, Object value) {
    synchronized (attributes) {
      return attributes.putIfAbsent(key, value);
    }
  }

  @Override
  public Object removeAttribute(Object key) {
    synchronized (attributes) {
      return attributes.remove(key);
    }
  }

  @Override
  public SocketAddress getRemoteAddress() {
    return remoteAddr;
  }

  @Override
  public SocketAddress getLocalAddress() {
    Channel channel = (context == null) ? null : context.channel();
    return (channel == null) ? null : channel.localAddress();
  }

  @Override
  public SocketAddress getAcceptanceAddress() {
    return acceptanceAddress;
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
  public IoService getService() {
    return service;
  }

  @Override // see SSHD-902
  public void shutdownOutputStream() throws IOException {
    Channel ch = context.channel();
    boolean debugEnabled = log.isDebugEnabled();
    if (!(ch instanceof AbstractNioChannel)) {
      if (debugEnabled) {
        log.debug("shudownOutputStream({}) channel is not AbstractNioChannel: {}",
            this, (ch == null) ? null : ch.getClass().getSimpleName());
      }
      return;
    }

    if (NIO_JAVA_CHANNEL_METHOD == null) {
      if (debugEnabled) {
        log.debug("shudownOutputStream({}) missing channel access method", this);
      }
      return;
    }

    SelectableChannel channel;
    try {
      channel = (SelectableChannel) NIO_JAVA_CHANNEL_METHOD.invoke(ch, GenericUtils.EMPTY_OBJECT_ARRAY);
    } catch (Exception t) {
      Throwable e = ExceptionUtils.peelException(t);
      log.warn("shudownOutputStream({}) failed ({}) to retrieve embedded channel: {}",
          this, e.getClass().getSimpleName(), e.getMessage());
      return;
    }

    if (!(channel instanceof SocketChannel)) {
      if (debugEnabled) {
        log.debug("shudownOutputStream({}) not a SocketChannel: {}",
            this, (channel == null) ? null : channel.getClass().getSimpleName());
      }
      return;
    }

    Socket socket = ((SocketChannel) channel).socket();
    if (socket.isConnected() && (!socket.isClosed())) {
      if (debugEnabled) {
        log.debug("shudownOutputStream({})", this);
      }
      socket.shutdownOutput();
    }
  }

  @Override
  protected CloseFuture doCloseGracefully() {
    context.writeAndFlush(Unpooled.EMPTY_BUFFER)
        .addListener(ChannelFutureListener.CLOSE)
        .addListener(fut -> closeFuture.setClosed());
    return closeFuture;
  }

  @Override
  protected void doCloseImmediately() {
    context.close();
    super.doCloseImmediately();
  }

  protected void channelActive(ChannelHandlerContext ctx) throws Exception {
    context = ctx;
    Channel channel = ctx.channel();
    service.channelGroup.add(channel);
    service.sessions.put(id, WebSocketSession.this);
    prev = context.newPromise().setSuccess();
    remoteAddr = channel.remoteAddress();
    handler.sessionCreated(WebSocketSession.this);

    Attribute<IoConnectFuture> connectFuture = channel.attr(WebSocketService.CONNECT_FUTURE_KEY);
    IoConnectFuture future = connectFuture.get();
    if (future != null) {
      future.setSession(WebSocketSession.this);
    }
  }

  protected void channelInactive(ChannelHandlerContext ctx) throws Exception {
    service.sessions.remove(id);
    handler.sessionClosed(WebSocketSession.this);
    context = null;
  }

  protected void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
    ByteBuf buf = (ByteBuf) msg;
    try {
      handler.messageReceived(WebSocketSession.this, NettySupport.asReadable(buf));
    } finally {
      buf.release();
    }
  }

  protected void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
    handler.exceptionCaught(WebSocketSession.this, cause);
  }

  @Override
  public String toString() {
    return getClass().getSimpleName()
        + "[local=" + getLocalAddress()
        + ", remote=" + getRemoteAddress()
        + "]";
  }

  protected static class DefaultIoWriteFuture extends AbstractIoWriteFuture {
    public DefaultIoWriteFuture(Object id, Object lock) {
      super(id, lock);
    }
  }

  /**
   * Simple netty adapter to use as a bridge.
   */
  protected class Adapter extends SimpleChannelInboundHandler<Object> {
    private final WebSocketClientHandshaker handshaker;
    private ChannelPromise handshakeFuture;

    public Adapter(URI webSocketUri, String accessToken) {
      super();
      HttpHeaders headers = new DefaultHttpHeaders();
      headers.set("Authorization", "tunnel " + accessToken);
      this.handshaker = WebSocketClientHandshakerFactory.newHandshaker(
          webSocketUri,
          WebSocketVersion.V13,
          "tunnel-relay-client",
          true,
          headers);
    }

    @Override
    public void channelActive(ChannelHandlerContext ctx) throws Exception {
      // from websocket handler
      handshaker.handshake(ctx.channel());
    }

    @Override
    public void channelInactive(ChannelHandlerContext ctx) throws Exception {
      WebSocketSession.this.channelInactive(ctx);
    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
      if (!handshaker.isHandshakeComplete()) {
        this.channelRead0(ctx, msg);
      } else if (msg instanceof BinaryWebSocketFrame) {
        BinaryWebSocketFrame frame = (BinaryWebSocketFrame) msg;
        WebSocketSession.this.channelRead(ctx, frame.content());
      }
    }

    // from websocket handler
    @Override
    public void channelRead0(ChannelHandlerContext ctx, Object msg) throws Exception {
      Channel ch = ctx.channel();
      if (!handshaker.isHandshakeComplete()) {
        try {
          handshaker.finishHandshake(ch, (FullHttpResponse) msg);
          System.out.println("WebSocket Client connected!");
          handshakeFuture.setSuccess();
          WebSocketSession.this.channelActive(ctx);
        } catch (WebSocketHandshakeException e) {
          System.out.println("WebSocket Client failed to connect");
          handshakeFuture.setFailure(e);
        }
        return;
      }

      if (msg instanceof FullHttpResponse) {
        FullHttpResponse response = (FullHttpResponse) msg;
        throw new IllegalStateException(
            "Unexpected FullHttpResponse (getStatus=" + response.status() +
                ", content=" + response.content().toString(CharsetUtil.UTF_8) + ')');
      }
    }

    // from websocket handler
    public ChannelFuture handshakeFuture() {
      return handshakeFuture;
    }

    // from websocket handler
    @Override
    public void handlerAdded(ChannelHandlerContext ctx) {
      handshakeFuture = ctx.newPromise();
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
      WebSocketSession.this.exceptionCaught(ctx, cause);
    }
  }

  /**
   * Simple netty adapter to use as a bridge.
   */
  protected class InboundAdapter extends ChannelInboundHandlerAdapter {

    public InboundAdapter() {
      super();
    }

    @Override
    public void channelActive(ChannelHandlerContext ctx) throws Exception {
      WebSocketSession.this.channelActive(ctx);
    }

    @Override
    public void channelInactive(ChannelHandlerContext ctx) throws Exception {
      WebSocketSession.this.channelInactive(ctx);
    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {

      BinaryWebSocketFrame frame = (BinaryWebSocketFrame) msg;
      WebSocketSession.this.channelRead(ctx, frame.content());

    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) throws Exception {
      WebSocketSession.this.exceptionCaught(ctx, cause);
    }
  }
}
