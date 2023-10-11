// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessControl;
import com.microsoft.tunnels.contracts.TunnelAccessControlEntry;
import com.microsoft.tunnels.contracts.TunnelAccessControlEntryType;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.contracts.TunnelProtocol;
import com.microsoft.tunnels.management.HttpResponseException;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import java.util.Arrays;
import java.util.concurrent.CompletableFuture;

import org.junit.Test;

/**
 * TunnelManagementClient tests.
 */
public class TunnelManagementClientTests extends TunnelTest {

  @Test
  public void createTunnel() {
    // Set up tunnel access control.
    var tunnelAccessEntry = new TunnelAccessControlEntry();
    tunnelAccessEntry.type = TunnelAccessControlEntryType.Anonymous;
    tunnelAccessEntry.subjects = new String[] {};
    tunnelAccessEntry.scopes = new String[] { "connect" };
    var access = new TunnelAccessControl();
    access.entries = new TunnelAccessControlEntry[] { tunnelAccessEntry };

    // set up the tunnel port.
    var port = new TunnelPort();
    port.portNumber = 3000;
    port.protocol = TunnelProtocol.https;

    // Set up tunnel.
    Tunnel tunnel = new Tunnel();
    tunnel.accessControl = access;
    tunnel.ports = new TunnelPort[] { port };

    // Configure tunnel request options.
    var requestOptions = new TunnelRequestOptions();
    requestOptions.tokenScopes = Arrays.asList(TunnelAccessScopes.host);
    requestOptions.includePorts = true;

    var createdTunnel = tryCreateTunnel(tunnel, requestOptions);

    assertNotNull(createdTunnel.clusterId);
    assertNotNull(createdTunnel.accessTokens);
    assertNotNull(createdTunnel.accessControl);
    assertNotNull(createdTunnel.created);
    assertNotNull(createdTunnel.description);
    assertNull(createdTunnel.domain);
    assertNotNull(createdTunnel.endpoints);
    assertNotNull(createdTunnel.name);
    assertNotNull(createdTunnel.options);
    assertNotNull(createdTunnel.ports);
    assertNotNull(createdTunnel.status);
    assertNotNull(createdTunnel.labels);
    assertNotNull(createdTunnel.tunnelId);

    tunnelManagementClient.deleteTunnelAsync(createdTunnel).join();
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
    port.protocol = TunnelProtocol.https;

    var tunnel = new Tunnel();
    var options = new TunnelRequestOptions();

    var createdTunnel = tryCreateTunnel(tunnel, options);
    assertNull("Tunnel should have been created with no ports.", createdTunnel.ports);

    var result = tunnelManagementClient.createTunnelPortAsync(createdTunnel, port, options).join();
    // Expect properties specified at creation to be equal.
    assertEquals("Expected ports to be equal.", portNumber, result.portNumber);
    assertEquals("Expected protocol to be equal.", TunnelProtocol.https, result.protocol);

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
