package com.microsoft.tunnels;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.management.HttpResponseException;
import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import org.junit.Test;

/**
 * TODO: tests E2E client connection.
 */
public class ConnectionTest {
  private static ProductHeaderValue userAgent = new ProductHeaderValue("connection-test",
      ConnectionTest.class.getPackage().getSpecificationVersion());

  private TunnelManagementClient tunnelManagementClient = new TunnelManagementClient(
      userAgent, () -> "");

  @Test
  public void createTunnelTest() {
    Tunnel tunnel = new Tunnel();
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);

    assertNotNull("Tunnel should not be null", createdTunnel);
    assertNotNull("Tunnel ID should not be null", createdTunnel.tunnelId);
    tunnelManagementClient.deleteTunnelAsync(createdTunnel, options).join();
  }

  @Test
  public void getTunnelTest() {
    Tunnel tunnel = new Tunnel();
    var options = new TunnelRequestOptions();
    var createdTunnel = tryCreateTunnel(tunnel, options);

    var result = tunnelManagementClient.getTunnelAsync(createdTunnel, options).join();

    assertNotNull("Tunnel should not be null", result);
    assertEquals(
        "Incorrect tunnel ID. Actual: " + result.tunnelId + " Expected: " + createdTunnel.tunnelId,
        result.tunnelId, createdTunnel.tunnelId);
    assertNotNull("Tunnel ID should not be null", result.tunnelId);

    tunnelManagementClient.deleteTunnelAsync(createdTunnel, options).join();
  }

  /**
   * Attempts to create the test tunnel. If the tunnel already exists, it will
   * delete and re-create it.
   *
   * @param tunnel  {@link Tunnel}
   * @param options {@link TunnelRequestOptions}
   * @return The created Tunnel
   */
  private Tunnel tryCreateTunnel(Tunnel tunnel, TunnelRequestOptions options) {
    return tunnelManagementClient.createTunnelAsync(tunnel, options).exceptionally(err -> {
      // if test tunnel already exists, delete and resend create request.
      if (err.getCause() instanceof HttpResponseException
          && ((HttpResponseException) err.getCause()).statusCode == 409) {
        tunnelManagementClient.deleteTunnelAsync(tunnel, options).join();
        return tunnelManagementClient.createTunnelAsync(tunnel, options).join();
      }
      throw new Error(err.getCause());
    }).join();
  }
}
