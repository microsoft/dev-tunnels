package com.microsoft.tunnels.connections;

public class ForwardedPort {
  public int localPort;
  public int remotePort;

  public ForwardedPort(int localPort, int remotePort) {
    this.localPort = localPort;
    this.remotePort = remotePort;
  }
}
