// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.websocket;

import io.netty.channel.EventLoopGroup;

import java.net.URI;
import java.util.Objects;

import org.apache.sshd.common.FactoryManager;
import org.apache.sshd.common.io.AbstractIoServiceFactoryFactory;
import org.apache.sshd.common.io.IoServiceFactory;

public class WebSocketServiceFactoryFactory extends AbstractIoServiceFactoryFactory {

  protected final EventLoopGroup eventLoopGroup;
  protected final URI webSocketUri;
  protected final String accessToken;

  public WebSocketServiceFactoryFactory() {
    this(null, null, null);
  }

  public WebSocketServiceFactoryFactory(URI webSocketUri, String accessToken) {
    this(null, webSocketUri, accessToken);
  }

  /**
   * Creates the WebSocketServiceFactory.
   */
  public WebSocketServiceFactoryFactory(
      EventLoopGroup eventLoopGroup,
      URI webSocketUri,
      String accessToken) {
    super(null);
    this.eventLoopGroup = eventLoopGroup;
    this.webSocketUri = webSocketUri;
    this.accessToken = accessToken;
  }

  @Override
  public IoServiceFactory create(FactoryManager manager) {
    Objects.requireNonNull(manager, "No factory manager provided");
    IoServiceFactory factory = new WebSocketServiceFactory(
        eventLoopGroup, webSocketUri, accessToken);
    factory.setIoServiceEventListener(manager.getIoServiceEventListener());
    return factory;
  }
}
