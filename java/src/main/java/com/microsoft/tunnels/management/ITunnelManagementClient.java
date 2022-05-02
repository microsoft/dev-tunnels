package com.microsoft.tunnels.management;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.contracts.TunnelRelayTunnelEndpoint;

import java.util.Collection;
import java.util.concurrent.CompletableFuture;

/**
 * Interface for a client that manages tunnels and tunnel ports
 * via the tunnel service management API.
 */
public interface ITunnelManagementClient {
  /**
   * Lists all tunnels that are owned by the caller.
   *
   * @param clusterId A tunnel cluster ID, or null to list tunnels globally.
   * @param options   Request options.
   * @return Array of tunnel objects.
   */
  public CompletableFuture<Collection<Tunnel>> listTunnelsAsync(
      String clusterId,
      TunnelRequestOptions options);

  /**
   * Search for all tunnels with matching tags.
   *
   * @param tags           The tags to search for.
   * @param requireAllTags requires a tunnel to have all specified tags.
   * @param clusterId      A tunnel cluster ID, or null to list tunnels globally.
   * @param domain         Tunnel domain, or null for the default domain.
   * @param options        Request options.
   * @return Array of tunnel objects.
   */
  public CompletableFuture<Collection<Tunnel>> searchTunnelsAsync(
      String[] tags,
      boolean requireAllTags,
      String clusterId,
      String domain,
      TunnelRequestOptions options);

  /**
   * Gets one tunnel by ID or name.
   *
   * @param tunnel  Tunnel object including at least either a tunnel name
   *                (globally unique, if configured) or tunnel ID and cluster ID.
   * @param options Request options.
   * @return The requested tunnel object, or null if the ID or name was not found.
   */
  public CompletableFuture<Tunnel> getTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

  /**
   * Creates a tunnel.
   *
   * @param tunnel  Tunnel object including all required properties.
   * @param options Request options.
   * @return The created tunnel object.
   */
  public CompletableFuture<Tunnel> createTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

  /**
   * Updates properties of a tunnel.
   *
   * @param tunnel  Tunnel object including at least either a tunnel name
   *                (globally unique, if configured) or tunnel ID and cluster ID.
   *                Any non-null properties on the object will be updated;
   *                null properties will not be modified.
   * @param options Request options.
   * @return Updated tunnel object, including both updated and unmodified
   *         properties.
   */
  public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

  /**
   * Deletes a tunnel.
   *
   * @param tunnel  Tunnel object including at least either a tunnel name
   *                (globally unique, if configured) or tunnel ID and cluster ID.
   * @param options Request options.
   * @return True if the tunnel was deleted; false if it was not found.
   */
  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

  /**
   * Creates or updates an endpoint for the tunnel.
   *
   * <p>
   * Note: A tunnel endpoint specifies how and where hosts and clients can connect
   * to a
   * tunnel.
   * Hosts create one or more endpoints when they start accepting connections on a
   * tunnel,
   * and delete the endpoints when they stop accepting connections.
   * </p>
   *
   * @param tunnel   Tunnel object including at least either a tunnel name
   *                 (globally unique, if configured) or tunnel ID and cluster ID.
   * @param endpoint Endpoint object to add or update, including at least
   *                 connection mode and host ID properties.
   * @param options  Request options.
   * @return The created or updated tunnel endpoint, with any server-supplied
   *         properties filled.
   */
  public CompletableFuture<TunnelRelayTunnelEndpoint> updateTunnelEndpointsAsync(
      Tunnel tunnel,
      TunnelRelayTunnelEndpoint endpoint,
      TunnelRequestOptions options);

  /**
   * Deletes a tunnel endpoint.
   *
   * <p>
   * Hosts create one or more endpoints when they start accepting connections on a
   * tunnel,
   * and delete the endpoints when they stop accepting connections.
   * </p>
   *
   * @param tunnel               Tunnel object including at least either a tunnel
   *                             name
   *                             (globally unique, if configured) or tunnel ID and
   *                             cluster ID.
   * @param hostId               Required ID of the host for endpoint(s) to be
   *                             deleted.
   * @param tunnelConnectionMode Optional connection mode for endpoint(s) to be
   *                             deleted,
   *                             or null to delete endpoints for all connection
   *                             modes.
   * @param options              Request options.
   * @return True if one or more endpoints were deleted, false if none were found.
   */
  public CompletableFuture<Boolean> deleteTunnelEndpointsAsync(
      Tunnel tunnel,
      String hostId,
      TunnelConnectionMode tunnelConnectionMode,
      TunnelRequestOptions options);

  /**
   * Lists all ports on a tunnel.
   *
   * @param tunnel  Tunnel object including at least either a tunnel name
   *                (globally unique, if configured) or tunnel ID and cluster ID.
   * @param options Request options.
   * @return Array of tunnel port objects.
   */
  public CompletableFuture<Collection<TunnelPort>> listTunnelPortsAsync(
      Tunnel tunnel,
      TunnelRequestOptions options);

  /**
   * Gets one port on a tunnel by port number.
   *
   * @param tunnel     Tunnel object including at least either a tunnel name
   *                   (globally unique, if configured) or tunnel ID and cluster
   *                   ID.
   * @param portNumber Port number.
   * @param options    Request options.
   * @return The requested tunnel port object, or null if the port number
   *         was not found.
   */
  public CompletableFuture<TunnelPort> getTunnelPortAsync(
      Tunnel tunnel,
      int portNumber,
      TunnelRequestOptions options);

  /**
   * Creates a tunnel port.
   *
   * @param tunnel     Tunnel object including at least either a tunnel name
   *                   (globally unique, if configured) or tunnel ID and cluster
   *                   ID.
   * @param tunnelPort Tunnel port object including all required properties.
   * @param options    Request options.
   * @return The created tunnel port object.
   */
  public CompletableFuture<TunnelPort> createTunnelPortAsync(
      Tunnel tunnel,
      TunnelPort tunnelPort,
      TunnelRequestOptions options);

  /**
   * Updates properties of a tunnel port.
   *
   * @param tunnel     Tunnel object including at least either a tunnel name
   *                   (globally unique, if configured) or tunnel ID and cluster
   *                   ID.
   * @param tunnelPort Tunnel port object including at least a port number.
   *                   Any additional non-null properties on the object will be
   *                   updated; null properties
   *                   will not be modified.
   * @param options    Request options.
   * @return Updated tunnel port object, including both updated and unmodified
   *         properties.
   */
  public CompletableFuture<TunnelPort> updateTunnelPortAsync(
      Tunnel tunnel,
      TunnelPort tunnelPort,
      TunnelRequestOptions options);

  /**
   * Deletes a tunnel port.
   *
   * @param tunnel     Tunnel object including at least either a tunnel name
   *                   (globally unique, if configured) or tunnel ID and cluster
   *                   ID.
   * @param portNumber Port number of the port to delete.
   * @param options    Request options.
   * @return True if the tunnel port was deleted; false if it was not found.
   */
  public CompletableFuture<Boolean> deleteTunnelPortAsync(
      Tunnel tunnel,
      int portNumber,
      TunnelRequestOptions options);
}
