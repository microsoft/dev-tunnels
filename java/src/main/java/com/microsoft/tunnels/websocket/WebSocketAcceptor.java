package com.microsoft.tunnels.websocket;

import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.socket.SocketChannel;

import java.net.SocketAddress;

import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.common.io.IoServiceEventListener;
import org.apache.sshd.common.util.GenericUtils;
import org.apache.sshd.netty.NettyIoAcceptor;

public class WebSocketAcceptor extends NettyIoAcceptor {
  private WebSocketSession webSocketSession;

  /**
   * Modifies the NettyIoAcceptor to create a websocket session.
   */
  public WebSocketAcceptor(WebSocketServiceFactory factory, IoHandler handler) {
    super(factory, handler);
    bootstrap.childHandler(new ChannelInitializer<SocketChannel>() {
      @Override
      public void initChannel(SocketChannel ch) throws Exception {
        IoServiceEventListener listener = getIoServiceEventListener();
        SocketAddress local = ch.localAddress();
        SocketAddress remote = ch.remoteAddress();
        SocketAddress service = GenericUtils.head(boundAddresses.keySet());
        try {
          if (listener != null) {
            try {
              listener.connectionAccepted(WebSocketAcceptor.this, local, remote, service);
            } catch (Exception e) {
              ch.close();
              throw e;
            }
          }

          ChannelPipeline p = ch.pipeline();
          webSocketSession = new WebSocketSession(WebSocketAcceptor.this, handler, service,
              factory.webSocketUri, factory.accessToken);
          p.addLast(webSocketSession.webSocketInboundAdapter);
        } catch (Exception e) {
          if (listener != null) {
            try {
              listener.abortAcceptedConnection(WebSocketAcceptor.this, local, remote, service, e);
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
}
