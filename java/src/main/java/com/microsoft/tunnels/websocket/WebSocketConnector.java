// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.websocket;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.socket.SocketChannel;
import io.netty.handler.codec.http.HttpClientCodec;
import io.netty.handler.codec.http.HttpObjectAggregator;
import io.netty.handler.codec.http.websocketx.extensions.compression.WebSocketClientCompressionHandler;
import io.netty.handler.ssl.SslContext;
import io.netty.handler.ssl.SslContextBuilder;
import io.netty.handler.ssl.SslHandler;
import io.netty.handler.ssl.util.InsecureTrustManagerFactory;

import java.net.SocketAddress;
import java.net.URI;

import org.apache.sshd.common.AttributeRepository;
import org.apache.sshd.common.io.IoConnectFuture;
import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoServiceEventListener;
import org.apache.sshd.netty.NettyIoConnector;

public class WebSocketConnector extends NettyIoConnector {
  protected final URI webSocketUri;
  protected final String accessToken;
  protected WebSocketSession webSocketSession;

  /**
   * Modifies the NettyIoConnector to create a websocket session.
   */
  public WebSocketConnector(WebSocketServiceFactory factory, IoHandler handler) {
    super(factory, handler);
    this.webSocketUri = factory.webSocketUri;
    this.accessToken = factory.accessToken;
    webSocketSession = new WebSocketSession(
        WebSocketConnector.this,
        handler,
        null, /* acceptanceAddress */
        factory.webSocketUri,
        factory.accessToken);

    bootstrap.handler(new ChannelInitializer<SocketChannel>() {
      @Override
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
            SslContext sslContext = SslContextBuilder.forClient()
                .trustManager(InsecureTrustManagerFactory.INSTANCE).build();
            p.addLast("ssl", new SslHandler(sslContext.newEngine(ch.alloc())));
          }
          p.addLast(new HttpClientCodec());
          p.addLast(new HttpObjectAggregator(8192));
          p.addLast(WebSocketClientCompressionHandler.INSTANCE);
          p.addLast(webSocketSession.webSocketConnectionHandler);
        } catch (Exception e) {
          if (listener != null) {
            try {
              listener.abortEstablishedConnection(
                  WebSocketConnector.this,
                  local,
                  context,
                  remote,
                  e);
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
  public IoConnectFuture connect(
      SocketAddress address,
      AttributeRepository context,
      SocketAddress localAddress) {
    boolean debugEnabled = log.isDebugEnabled();
    if (debugEnabled) {
      log.debug("Connecting to {}", address);
    }

    IoConnectFuture future = new DefaultIoConnectFuture(address, null);
    var relayPort = webSocketUri.getPort();
    if (relayPort == -1) {
      if (webSocketUri.getScheme().equals("wss")) {
        relayPort = 443;
      } else if (webSocketUri.getScheme().equals("ws")) {
        relayPort = 80;
      } else {
        throw new IllegalStateException("Unexpected tunnel relay client uri scheme: " + webSocketUri);
      }
    }
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
}
