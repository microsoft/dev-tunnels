// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

import com.microsoft.tunnels.contracts.ResourceStatus;
import com.microsoft.tunnels.contracts.TunnelAccessControl;
import com.microsoft.tunnels.contracts.TunnelConstraints;
import com.microsoft.tunnels.contracts.TunnelContracts;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelServiceProperties;

import java.net.URI;
import java.util.Arrays;

import org.junit.Test;

/**
 * Unit test for the static contracts classes.
 *
 * Tests should call from the generated contracts (insread of from the
 * *Statics.java) classes
 * to ensure the contracts have been correctly generated and linked to the
 * handwritten files.
 */
public class TunnelContractsTests {
  @Test
  public void tunnelConstraints() {
    assertTrue(TunnelConstraints.isValidClusterId("usw2"));
    assertFalse(TunnelConstraints.isValidClusterId("usw$2")); // unallowed special character

    assertTrue(TunnelConstraints.isValidTunnelId("bcd123fg"));
    assertFalse(TunnelConstraints.isValidTunnelId("bcd123fgh")); // id too long

    assertTrue(TunnelConstraints.isValidTunnelName("mytunnel"));
    assertFalse(TunnelConstraints.isValidTunnelName("my$unnel")); // unallowed special character

    assertTrue(TunnelConstraints.isValidTunnelIdOrName("mytunnel"));
    assertTrue(TunnelConstraints.isValidTunnelIdOrName("bcd123fg"));
    assertFalse(TunnelConstraints.isValidTunnelIdOrName("my$unnel"));

    assertTrue(TunnelConstraints.isValidTunnelTag("my-tunnel-tag"));
    assertFalse(TunnelConstraints.isValidTunnelTag("my-tunnel&tag")); // unallowed specialcharacter

    assertNotNull(TunnelConstraints.validateTunnelId("bcdf123g", null));
    assertNotNull(TunnelConstraints.validateTunnelIdOrName("mytunnel", null));
  }

  @Test
  public void tunnelProperties() {
    var prod = TunnelServiceProperties.environment("prod");
    var dev = TunnelServiceProperties.environment("dev");
    var ppe = TunnelServiceProperties.environment("ppe");
    assertTrue(prod != null && prod instanceof TunnelServiceProperties);
    assertTrue(dev != null && dev instanceof TunnelServiceProperties);
    assertTrue(ppe != null && ppe instanceof TunnelServiceProperties);
  }

  @Test
  public void tunnelEndpoint() {
    TunnelEndpoint endpoint = new TunnelEndpoint();
    endpoint.portUriFormat = "{port}.test.com";
    URI uri = TunnelEndpoint.getPortUri(endpoint, 3000);
    assertNotNull(uri);
    assertTrue(uri.toString().equals("3000.test.com"));
  }

  @Test
  public void tunnelAccessControl() {
    var scopes = Arrays.asList("connect", "host");
    var validScopes = Arrays.asList("connect", "create", "inspect", "host", "manage");
    var invalidScopes = Arrays.asList("connect", "invalid");
    TunnelAccessControl.validateScopes(scopes, null);
    TunnelAccessControl.validateScopes(validScopes, null);
    TunnelAccessControl.validateScopes(scopes, validScopes);
    Exception exception = assertThrows(IllegalArgumentException.class, ()-> {
      TunnelAccessControl.validateScopes(invalidScopes, validScopes);
    });
    assertTrue(exception.getMessage().equals("Invalid tunnel access scope: invalid"));
  }

  @Test
  public void resourceStatus() {
    var gson = TunnelContracts.getGson();
    var result1 = gson.fromJson("{\"current\": 3, \"limit\": 10 }", ResourceStatus.class);
    assertNotEquals(0, result1.current);
    assertNotEquals(0, result1.limit);
    var result2 = gson.fromJson("3", ResourceStatus.class);
    assertEquals(result1.current, result2.current);
  }
}
