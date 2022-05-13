// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.websocket;

import io.netty.channel.EventLoopGroup;

import java.net.URI;

import org.apache.sshd.common.io.IoAcceptor;
import org.apache.sshd.common.io.IoConnector;
import org.apache.sshd.common.io.IoHandler;
import org.apache.sshd.netty.NettyIoAcceptor;
import org.apache.sshd.netty.NettyIoServiceFactory;

public class WebSocketServiceFactory extends NettyIoServiceFactory {

  protected final URI webSocketUri;
  protected final String accessToken;

  public WebSocketServiceFactory() {
    this(null, null, null);
  }

  /**
   * Creates the websocket connector and acceptor.
   */
  public WebSocketServiceFactory(EventLoopGroup group, URI webSocketUri, String accessToken) {
    super(group);
    this.webSocketUri = webSocketUri;
    this.accessToken = accessToken;
  }

  public EventLoopGroup getEventLoopGroup() {
    return this.eventLoopGroup;
  }

  @Override
  public IoConnector createConnector(IoHandler handler) {
    return new WebSocketConnector(this, handler);
  }

  @Override
  public IoAcceptor createAcceptor(IoHandler handler) {
    return new NettyIoAcceptor(this, handler);
  }
}
