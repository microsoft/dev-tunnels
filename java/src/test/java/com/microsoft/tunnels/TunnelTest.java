// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import java.util.Arrays;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;

import org.apache.maven.shared.utils.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Base class for java tunnel tests
 */
public abstract class TunnelTest {
  protected static ProductHeaderValue userAgent = new ProductHeaderValue("connection-test",
      TunnelManagementClientTests.class.getPackage().getSpecificationVersion());

  // Test tunnel used for local testing of tunnel connections.
  protected static final String testTunnelName = System.getenv("TEST_TUNNEL_NAME");
  // User token for creating tunnels in local tests.
  protected static final String testToken = System.getenv("TEST_TUNNEL_TOKEN");

  protected static final Supplier<CompletableFuture<String>> userTokenCallback = StringUtils
      .isNotBlank(testToken)
          ? () -> CompletableFuture.completedFuture(testToken)
          : null;

  protected static TunnelManagementClient tunnelManagementClient = new TunnelManagementClient(
      new ProductHeaderValue[] { userAgent },
      userTokenCallback,
      "2023-09-27-preview");

  protected static final Logger logger = LoggerFactory.getLogger(TunnelTest.class);

  protected TunnelTest() {
    if (StringUtils.isNotBlank(System.getenv("TEST_TUNNEL_VERBOSE"))) {
      enableVerboseLogging();
    }
  }

  /**
   * Enables FINE logging for all components (including HTTP, SSL, SSH). VERY
   * verbose.
   */
  private static void enableVerboseLogging() {
    // SLF4J logging is routed to java.util.logging via the reference to the
    // slf4j-jdk14 package.
    var rootLogger = java.util.logging.Logger.getLogger("");
    rootLogger.setLevel(java.util.logging.Level.FINE);

    // A console log handler is enabled at the root level by default.
    for (var logHandler : rootLogger.getHandlers()) {
      logHandler.setLevel(java.util.logging.Level.ALL);
    }
  }

  protected static Tunnel getTestTunnel() {
    // Set up tunnel
    Tunnel tunnel = new Tunnel();
    tunnel.name = testTunnelName;

    // Configure tunnel request options
    var requestOptions = new TunnelRequestOptions();
    requestOptions.tokenScopes = Arrays.asList(TunnelAccessScopes.connect);
    requestOptions.includePorts = true;

    // get tunnel
    var result = tunnelManagementClient.getTunnelAsync(tunnel, requestOptions).join();
    return result;
  }
}
