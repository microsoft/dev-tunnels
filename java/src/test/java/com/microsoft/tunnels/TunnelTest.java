// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels;

import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;

import java.util.function.Supplier;

import org.apache.maven.shared.utils.StringUtils;

/**
 * Base class for java tunnel tests
 */
public abstract class TunnelTest {
  protected static ProductHeaderValue userAgent = new ProductHeaderValue("connection-test",
      TunnelManagementClientTests.class.getPackage().getSpecificationVersion());

  // Test tunnel used for local testing of tunnel connections.
  protected final String testTunnelName = System.getenv("TEST_TUNNEL");
  // User token for creating tunnels in local tests.
  protected final String testToken = System.getenv("TUNNELS_TOKEN");

  protected final Supplier<String> userTokenCallback = StringUtils.isNotBlank(testToken)
      ? () -> testToken
      : null;

  protected TunnelManagementClient tunnelManagementClient = new TunnelManagementClient(
      new ProductHeaderValue[] { userAgent },
      userTokenCallback);
}
