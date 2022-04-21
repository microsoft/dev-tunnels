package com.microsoft.tunnels.connections;

public class ForwardedPort {
  public Integer localPort;
  public Integer remotePort;

  public ForwardedPort(Integer localPort, Integer remotePort) {
    this.localPort = localPort;
    this.remotePort = remotePort;
  }
}
