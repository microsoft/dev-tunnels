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
 * Used for local manual testing.
 * Requires AUTH_TOKEN to be set with a token from `basis user show -v`.
 */
public class ConnectionTest {
  private static final String AUTH_TOKEN = "AUTH_TOKEN";
  private static ProductHeaderValue userAgent = new ProductHeaderValue("connection-test",
      ConnectionTest.class.getPackage().getSpecificationVersion());

  private TunnelManagementClient tunnelManagementClient = new TunnelManagementClient(userAgent,
      () -> "Bearer " + AUTH_TOKEN);

  @Test
  public void createTunnelTest() {
    Tunnel tunnel = new Tunnel("testtunnel");
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);

    assertNotNull("Tunnel should not be null", createdTunnel);
    assertEquals("Incorrect tunnel name. Actual: " + createdTunnel.name + " Expected: " + tunnel.name,
        createdTunnel.name, tunnel.name);
    assertNotNull("Tunnel ID should not be null", createdTunnel.tunnelId);
    tunnelManagementClient.deleteTunnelAsync(tunnel, options).join();
  }

  @Test
  public void getTunnelTest() {
    Tunnel tunnel = new Tunnel("testtunnel");
    var options = new TunnelRequestOptions();
    tryCreateTunnel(tunnel, options);

    var result = tunnelManagementClient.getTunnelAsync(tunnel, options).join();

    assertNotNull("Tunnel should not be null", result);
    assertEquals("Incorrect tunnel name. Actual: " + result.name + " Expected: " + tunnel.name,
        result.name, tunnel.name);
    assertNotNull("Tunnel ID should not be null", result.tunnelId);

    tunnelManagementClient.deleteTunnelAsync(tunnel, options).join();
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
