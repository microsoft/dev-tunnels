package com.microsoft.tunnels;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.contracts.TunnelProtocol;
import com.microsoft.tunnels.management.HttpResponseException;
import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import java.util.concurrent.CompletableFuture;
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
  public void createTunnel() {
    Tunnel tunnel = new Tunnel();
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);

    assertNotNull(createdTunnel.clusterId);
    assertNotNull(createdTunnel.accessTokens);
    assertNotNull(createdTunnel.created);
    assertNotNull(createdTunnel.description);
    assertNull(createdTunnel.domain);
    assertNotNull(createdTunnel.endpoints);
    assertNotNull(createdTunnel.name);
    assertNotNull(createdTunnel.options);
    assertNull(createdTunnel.ports);
    assertNotNull(createdTunnel.status);
    assertNotNull(createdTunnel.tags);
    assertNotNull(createdTunnel.tunnelId);

    tunnelManagementClient.deleteTunnelAsync(createdTunnel, options).join();
  }

  @Test
  public void getTunnel() {
    Tunnel tunnel = new Tunnel();
    var options = new TunnelRequestOptions();
    var createdTunnel = tryCreateTunnel(tunnel, options);

    var result = tunnelManagementClient.getTunnelAsync(createdTunnel, options).join();

    assertEquals(
        "Incorrect tunnel ID. Actual: " + result.tunnelId + " Expected: " + createdTunnel.tunnelId,
        result.tunnelId, createdTunnel.tunnelId);
    assertNotNull("Tunnel ID should not be null", result.tunnelId);

    tunnelManagementClient.deleteTunnelAsync(createdTunnel, options).join();
  }

  @Test
  public void updateTunnelAsync() {
    Tunnel tunnel = new Tunnel();
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);
    assertEquals("Created tunnel description should be blank.", createdTunnel.description, "");
    var description = "updated";
    createdTunnel.description = description;

    var updatedTunnel = tunnelManagementClient.updateTunnelAsync(createdTunnel, options).join();
    assertEquals("Tunnel description should have been updated.",
        updatedTunnel.description,
        description);
    tunnelManagementClient.deleteTunnelAsync(createdTunnel, options).join();
  }

  @Test
  public void createTunnelPort() {
    var port = new TunnelPort();
    var portNumber = 3000;
    port.portNumber = portNumber;
    port.protocol = TunnelProtocol.Https;

    var tunnel = new Tunnel();
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);
    assertNull("Tunnel should have been created with no ports.", createdTunnel.ports);

    var result = tunnelManagementClient.createTunnelPortAsync(createdTunnel, port, options).join();
    // Expect properties specified at creation to be equal.
    assertEquals("Expected ports to be equal.", portNumber, result.portNumber);
    assertEquals("Expected protocol to be equal.", TunnelProtocol.Https, result.protocol);

    // Expect unspecified properties to have been initialized to defaults.
    assertNull(result.accessTokens);
    assertNotNull(result.clusterId);
    assertNotNull(result.options);
    assertNotNull(result.status);
    assertNotNull(result.tunnelId);
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
    CompletableFuture<Tunnel> result = tunnelManagementClient.createTunnelAsync(tunnel, options)
        .exceptionally(err -> {
          // if test tunnel already exists, delete and resend create request.
          if (err.getCause() instanceof HttpResponseException
              && ((HttpResponseException) err.getCause()).statusCode == 409) {
            tunnelManagementClient.deleteTunnelAsync(tunnel, options).join();
            return tunnelManagementClient.createTunnelAsync(tunnel, options).join();
          }
          throw new Error(err.getCause());
        });
    Tunnel createdTunnel = result.join();
    assertNotNull("Expected created tunnel to not be null", createdTunnel);
    return createdTunnel;
  }
}
