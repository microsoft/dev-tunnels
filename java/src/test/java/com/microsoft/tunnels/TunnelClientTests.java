package com.microsoft.tunnels;

import com.microsoft.tunnels.connections.TunnelClient;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import java.util.Arrays;

import org.junit.Test;

public class TunnelClientTests extends TunnelTest {

  @Test
  public void connectClient() {
    // Set up tunnel
    Tunnel tunnel = new Tunnel();
    tunnel.name = this.testTunnelName;
    System.out.println(tunnel.name);

    // Configure tunnel request options
    var requestOptions = new TunnelRequestOptions();
    requestOptions.tokenScopes = Arrays.asList(TunnelAccessScopes.connect);
    requestOptions.includePorts = true;

    // get tunnel
    var result = this.tunnelManagementClient.getTunnelAsync(tunnel, requestOptions).join();

    // Connect to the tunnel
    TunnelClient client = new TunnelClient();

    // connect to the tunnel
    client.connect(result);
    client.stop();
  }
}
