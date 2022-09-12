// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

import com.microsoft.tunnels.contracts.Tunnel;
import java.util.concurrent.CompletableFuture;

/**
 * Interface for a client capable of making a connection to a tunnel and
 * forwarding ports over the tunnel.
 */
public interface TunnelClient {
  ForwardedPortsCollection getForwardedPorts();

  /**
   * Connects to the specified tunnel.
   *
   * @param tunnel Tunnel to connect to.
   * @return A future that completes when the connection succeeds or fails.
   */
  CompletableFuture<Void> connectAsync(Tunnel tunnel);

  /**
   * Connects to the specified tunnel and host.
   *
   * @param tunnel Tunnel to connect to.
   * @param hostId ID of the host connected to the tunnel.
   * @return A future that completes when the connection succeeds or fails.
   */
  CompletableFuture<Void> connectAsync(
      Tunnel tunnel,
      String hostId);
  /**
   * Sends a request to the host to refresh ports that were updated using the management API,
   * and waits for the refresh to complete.
   */
  CompletableFuture<Void> refreshPortsAsync();

  void stop();
}
