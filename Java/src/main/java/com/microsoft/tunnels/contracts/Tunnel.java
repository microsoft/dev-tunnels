package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Collection;
import java.util.HashMap;

/**
 * Data contract for tunnel objects managed through the tunnel service REST API.
 */
public class Tunnel {
  /**
   * Gets or sets the ID of the cluster the tunnel was created in.
   */
  @Expose
  public String clusterId;
  /**
   * Gets or sets the generated ID of the tunnel, unique within the cluster.
   */
  @Expose
  public String tunnelId;
  /**
   * Gets or sets the optional short name (alias) of the tunnel.
   * The name must be globally unique within the parent domain, and must be a
   * valid subdomain.
   */
  @Expose
  public String name;
  /**
   * Gets or sets the description of the tunnel.
   */
  @Expose
  public String description;
  /**
   * Gets or sets the tags of the tunnel.
   */
  @Expose
  public String[] tags;
  /**
   * Gets or sets the optional parent domain of the tunnel,
   * if it is not using the default parent domain.
   */
  @Expose
  public String domain;
  /**
   * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
   */
  @Expose
  public HashMap<String, String> accessTokens;
  /**
   * Gets or sets default options for the tunnel.
   */
  // TODO
  // public TunnelAccessControl accessControlEntry;
  @Expose
  public TunnelOptions options;
  // TODO
  // public TunnelStatus status;
  /**
   * Gets or sets an array of endpoints where hosts are currently accepting
   * client connections to the tunnel.
   */
  @Expose
  public Collection<TunnelEndpoint> endpoints;
  // TODO
  // public TunnelPort[] ports;
  // TODO
  // public DateTime created;

  public Tunnel() {
    this(null);
  }

  public Tunnel(String name) {
    this.name = name;
  }
}
