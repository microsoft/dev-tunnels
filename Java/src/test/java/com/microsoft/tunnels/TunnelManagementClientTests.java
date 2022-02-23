package com.microsoft.tunnels;

import static org.junit.Assert.assertNotEquals;

import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;

import org.junit.Test;

public class TunnelManagementClientTests {
  /**
   * constructs TunnelManagementClient.
   */
  @Test
  public void createTunnelManagementClient() {
    var userAgent = new ProductHeaderValue("java-sdk-test");
    var managementClient = new TunnelManagementClient(userAgent);
    assertNotEquals(managementClient, null);
  }
}
