package com.microsoft.tunnels.websocket;

import java.io.IOException;
import java.net.SocketAddress;
import java.net.URI;

import org.apache.sshd.client.SshClient;
import org.apache.sshd.client.config.hosts.HostConfigEntry;
import org.apache.sshd.client.future.ConnectFuture;
import org.apache.sshd.client.future.DefaultConnectFuture;
import org.apache.sshd.common.AttributeRepository;
import org.apache.sshd.common.future.SshFutureListener;
import org.apache.sshd.common.io.IoConnectFuture;
import org.apache.sshd.common.keyprovider.KeyIdentityProvider;

public class WebSocketSshClient extends SshClient {
  public WebSocketSshClient() {
    super();
  }

  public ConnectFuture connect() throws IOException {
    return doConnect(null, null, null, null);
  }

  // @Override
  // protected ConnectFuture doConnect(
  //     String username, SocketAddress targetAddress,
  //     AttributeRepository context, SocketAddress localAddress,
  //     KeyIdentityProvider identities, HostConfigEntry hostConfig)
  //     throws IOException {
  //   if (connector == null) {
  //     throw new IllegalStateException(
  //         "SshClient not started. Please call start() method before connecting to a server");
  //   }

  //   ConnectFuture connectFuture = new DefaultConnectFuture(username + "@" + targetAddress, null);
  //   SshFutureListener<IoConnectFuture> listener = createConnectCompletionListener(
  //       connectFuture, username, targetAddress, identities, hostConfig);
  //   IoConnectFuture connectingFuture = connector.connect(targetAddress, context, localAddress);
  //   connectingFuture.addListener(listener);
  //   return connectFuture;
  // }
}
