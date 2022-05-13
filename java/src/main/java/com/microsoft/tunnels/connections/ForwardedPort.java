// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

public class ForwardedPort {
  private int localPort;
  private int remotePort;

  public ForwardedPort(int localPort, int remotePort) {
    this.localPort = localPort;
    this.remotePort = remotePort;
  }

  public int getLocalPort() {
    return localPort;
  }

  public int getRemotePort() {
    return remotePort;
  }
}
