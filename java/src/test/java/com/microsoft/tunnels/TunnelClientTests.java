package com.microsoft.tunnels;

import com.microsoft.tunnels.connections.ForwardedPort;
import com.microsoft.tunnels.connections.ForwardedPortEventListener;
import com.microsoft.tunnels.connections.TunnelClient;
import com.microsoft.tunnels.connections.TunnelRelayTunnelClient;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.management.HttpResponseException;

import org.junit.Assert;
import org.junit.BeforeClass;
import org.junit.Test;

public class TunnelClientTests extends TunnelTest {
  private static Tunnel testTunnel;

  @BeforeClass
  public static void initializeTestTunnel() {
    testTunnel = getTestTunnel();
  }

  @Test
  public void connectClient() {
    TunnelClient client = new TunnelRelayTunnelClient();

    // connect to the tunnel
    client.connectAsync(testTunnel).thenRun(() -> {
      logger.info("TunnelRelayTunnel client connected successfully.");
      client.stop();
    }).join();
  }

  @Test
  public void addAndRemovePorts() {
    var testPort = new TunnelPort();
    testPort.portNumber = 5000;
    testPort.protocol = "https";
    TunnelRelayTunnelClient client = new TunnelRelayTunnelClient();

    client.getForwardedPorts().addListener(new ForwardedPortEventListener() {
      @Override
      public void onForwardedPortAdded(ForwardedPort port) {
        logger.info("Port added - local: " + port.getLocalPort()
            + " remote: " + port.getRemotePort());
        Assert.assertEquals(testPort.portNumber, port.getRemotePort());
      }

      @Override
      public void onForwardedPortRemoved(ForwardedPort port) {
        logger.info("Port removed - local: " + port.getLocalPort()
            + " remote: " + port.getRemotePort());
        Assert.assertEquals(testPort.portNumber, port.getRemotePort());
      }
    });

    // Add a new port via the tunnelManagementClient and verify that the client
    // does not pick it up.
    Runnable updateTunnelPort = () -> {
      Assert.assertEquals("Expected no ports to be forwarded yet.",
          0, client.getForwardedPorts().size());
      logger.info("Adding port " + testPort.portNumber + " to test tunnel " +
          testTunnelName);
      tunnelManagementClient.createTunnelPortAsync(testTunnel, testPort,
          null).thenRun(() -> {
            Assert.assertEquals("Expected forwarded ports to not be updated.",
                0, client.getForwardedPorts().size());
          });
    };

    // Verify that calling refreshPorts on the client adds new ports to the
    // collection.
    Runnable refreshTunnelPorts = () -> {
      logger.info("Refreshing ports of test tunnel " + testTunnelName);
      client.refreshPortsAsync().thenRun(() -> {
        Assert.assertEquals("Expected port " + testPort.portNumber
            + " to be added to the forwarded ports collection.",
            1, client.getForwardedPorts().size());
      });
    };

    try {
      client.connectAsync(testTunnel)
          .thenRun(updateTunnelPort)
          .thenRun(refreshTunnelPorts)
          .thenRun(refreshTunnelPorts) // calling refresh a second time shouldn't affect anything.
          .join();
    } finally {
      try {
        // Delete the port and verify that the correct port is removed from the collection.
        logger.info("Deleting port " + testPort.portNumber + " of test tunnel " + testTunnelName);
        tunnelManagementClient.deleteTunnelPortAsync(testTunnel, testPort.portNumber, null)
          .thenRun(refreshTunnelPorts)
          .join();
      } finally {
        client.stop();
      }
    }
  }
}
