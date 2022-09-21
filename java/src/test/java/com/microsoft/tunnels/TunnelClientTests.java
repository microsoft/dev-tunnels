package com.microsoft.tunnels;

import com.microsoft.tunnels.connections.ForwardedPort;
import com.microsoft.tunnels.connections.ForwardedPortEventListener;
import com.microsoft.tunnels.connections.TunnelClient;
import com.microsoft.tunnels.connections.TunnelRelayTunnelClient;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.management.HttpResponseException;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

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

    // Ensure the port was deleted on previous test.
    try {
      tunnelManagementClient.deleteTunnelPortAsync(testTunnel, testPort.portNumber, null).join();
    } catch (CompletionException e) {
      var cause = e.getCause();
      if (cause instanceof HttpResponseException
          && ((HttpResponseException) cause).statusCode != 404) {
        throw e;
      }
    }

    client.connectAsync(testTunnel).join();

    // Ensure that the test port is not being forwarded.
    logForwardedPorts(client);
    Assert.assertEquals("Expected no ports to be forwarded yet",
        0, client.getForwardedPorts().size());

    // Add a port using the management client
    logger.info("Adding port " + testPort.portNumber + " to test tunnel " + testTunnelName);
    tunnelManagementClient.createTunnelPortAsync(testTunnel, testPort, null).join();
    logForwardedPorts(client);

    // Verify that the local port is not updated without calling refreshPorts
    Assert.assertEquals("Expected forwarded ports to not be updated.",
        0, client.getForwardedPorts().size());
    logger.info("Refreshing ports of test tunnel " + testTunnelName);

    // Call refresh ports and verify that the port is updated.
    client.refreshPortsAsync().join();
    logForwardedPorts(client);
    Assert.assertEquals("Expected port " + testPort.portNumber
        + " to be added to the forwarded ports collection.", 1, client.getForwardedPorts().size());

    // Calling refresh with no new ports added should do nothing.
    client.refreshPortsAsync().join();
    Assert.assertEquals("Expected port " + testPort.portNumber
        + " to be added to the forwarded ports collection.",
        1, client.getForwardedPorts().size());

    // Delete the port and verify that the correct port is removed from the
    // collection.
    logger.info("Deleting port " + testPort.portNumber + " of test tunnel " + testTunnelName);
    tunnelManagementClient.deleteTunnelPortAsync(testTunnel, testPort.portNumber, null).join();
    client.refreshPortsAsync().join();
    logForwardedPorts(client);
    Assert.assertEquals("Expected port " + testPort.portNumber
        + " to be removed from the forwarded ports collection.",
        0, client.getForwardedPorts().size());

    client.stop();
  }

  private void logForwardedPorts(TunnelRelayTunnelClient tunnelClient) {
    var ports = tunnelClient.getForwardedPorts();
    String message = "Forwarded ports: [";
    for (ForwardedPort port : ports) {
      message += "{ local: " + port.getLocalPort() + ", remote: " + port.getRemotePort() + " },";
    }
    message += "]";
    logger.info(message);
  }
}
