package com.microsoft.tunnels.websocket;

import java.net.SocketAddress;
import java.net.URI;

import javax.net.ssl.SSLEngine;

import io.netty.bootstrap.Bootstrap;
import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelOption;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.group.DefaultChannelGroup;
import io.netty.channel.socket.SocketChannel;
import io.netty.channel.socket.nio.NioSocketChannel;
import io.netty.handler.codec.http.DefaultHttpHeaders;
import io.netty.handler.codec.http.HttpClientCodec;
import io.netty.handler.codec.http.HttpObjectAggregator;
import io.netty.handler.codec.http.websocketx.WebSocketClientHandshakerFactory;
import io.netty.handler.codec.http.websocketx.WebSocketVersion;
import io.netty.handler.codec.http.websocketx.extensions.compression.WebSocketClientCompressionHandler;
import io.netty.handler.logging.LogLevel;
import io.netty.handler.logging.LoggingHandler;
import io.netty.handler.ssl.SslContext;
import io.netty.handler.ssl.SslContextBuilder;
import io.netty.handler.ssl.SslHandler;
import io.netty.handler.ssl.util.InsecureTrustManagerFactory;
import io.netty.util.concurrent.GlobalEventExecutor;
import org.apache.sshd.common.AttributeRepository;
import org.apache.sshd.common.future.DefaultSshFuture;
import org.apache.sshd.common.io.IoConnectFuture;
import org.apache.sshd.common.io.IoConnector;
import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoServiceEventListener;
import org.apache.sshd.common.io.IoSession;
import org.apache.sshd.netty.NettyIoService;

public class WebSocketConnector extends WebSocketService implements IoConnector {
  // Shared across all connectors
  private static final LoggingHandler LOGGING_TRACE = new LoggingHandler(WebSocketConnector.class, LogLevel.TRACE);

  protected final Bootstrap bootstrap = new Bootstrap();
  protected final URI webSocketUri;
  protected WebSocketSession webSocketSession;

  public WebSocketConnector(WebSocketServiceFactory factory, IoHandler handler) {
    super(factory, handler);
    this.webSocketUri = factory.webSocketUri;
    webSocketSession = new WebSocketSession(
        WebSocketConnector.this,
        handler,
        null,
        factory.webSocketUri,
        factory.accessToken);

    channelGroup = new DefaultChannelGroup("sshd-connector-channels", GlobalEventExecutor.INSTANCE);
    bootstrap.group(factory.getEventLoopGroup())
        .channel(NioSocketChannel.class)
        .option(ChannelOption.SO_BACKLOG, 100) // TODO make this configurable
        .handler(new ChannelInitializer<SocketChannel>() {
          @Override
          @SuppressWarnings("synthetic-access")
          protected void initChannel(SocketChannel ch) throws Exception {
            IoServiceEventListener listener = getIoServiceEventListener();
            SocketAddress local = ch.localAddress();
            SocketAddress remote = ch.remoteAddress();
            AttributeRepository context = ch.hasAttr(CONTEXT_KEY)
                ? ch.attr(CONTEXT_KEY).get()
                : null;
            try {
              if (listener != null) {
                try {
                  listener.connectionEstablished(WebSocketConnector.this, local, context, remote);
                } catch (Exception e) {
                  ch.close();
                  throw e;
                }
              }

              if (context != null) {
                webSocketSession.setAttribute(AttributeRepository.class, context);
              }

              ChannelPipeline p = ch.pipeline();
              if (factory.webSocketUri.getScheme().equals("wss")) {
                // In some debugging scenarios secure websockets are not used.
                SslContext sslContext = SslContextBuilder.forClient()
                  .trustManager(InsecureTrustManagerFactory.INSTANCE).build();
                p.addLast("ssl", new SslHandler(sslContext.newEngine(ch.alloc())));
              }
              p.addLast(LOGGING_TRACE); // TODO make this configurable
              p.addLast(new HttpClientCodec(),
                  new HttpObjectAggregator(8192),
                  WebSocketClientCompressionHandler.INSTANCE,
                  webSocketSession.adapter);

            } catch (Exception e) {
              if (listener != null) {
                try {
                  listener.abortEstablishedConnection(WebSocketConnector.this, local, context, remote, e);
                } catch (Exception exc) {
                  if (log.isDebugEnabled()) {
                    log.debug("initChannel(" + ch + ") listener=" + listener
                        + " ignoring abort event exception",
                        exc);
                  }
                }
              }

              throw e;
            }
          }
        });
  }

  @Override
  public IoConnectFuture connect(SocketAddress address, AttributeRepository context, SocketAddress localAddress) {
    boolean debugEnabled = log.isDebugEnabled();
    if (debugEnabled) {
      log.debug("Connecting to {}", address);
    }

    IoConnectFuture future = new DefaultIoConnectFuture(address, null);
    var relayPort = webSocketUri.getHost().equals("localhost") ? 9921 : 443;
    ChannelFuture chf = bootstrap.connect(webSocketUri.getHost(), relayPort);

    Channel channel = chf.channel();
    channel.attr(CONNECT_FUTURE_KEY).set(future);
    if (context != null) {
      channel.attr(CONTEXT_KEY).set(context);
    }

    chf.addListener(cf -> {
      Throwable t = chf.cause();
      if (t != null) {
        future.setException(t);
      } else if (chf.isCancelled()) {
        future.cancel();
      }
    });
    return future;
  }

  public static class DefaultIoConnectFuture extends DefaultSshFuture<IoConnectFuture> implements IoConnectFuture {
    public DefaultIoConnectFuture(Object id, Object lock) {
      super(id, lock);
    }

    @Override
    public IoSession getSession() {
      Object v = getValue();
      return (v instanceof IoSession) ? (IoSession) v : null;
    }

    @Override
    public Throwable getException() {
      Object v = getValue();
      return (v instanceof Throwable) ? (Throwable) v : null;
    }

    @Override
    public boolean isConnected() {
      Object v = getValue();
      return v instanceof IoSession;
    }

    @Override
    public void setSession(IoSession session) {
      setValue(session);
    }

    @Override
    public void setException(Throwable exception) {
      setValue(exception);
    }
  }
}
