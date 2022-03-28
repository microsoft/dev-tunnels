package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Parameters for connecting to a tunnel via the tunnel service's built-in relay
 * function.
 */
public class TunnelRelayTunnelEndpoint extends TunnelEndpoint {
  /**
   * Gets or sets the host URI.
   */
  @Expose
  public String hostRelayUri;

  /**
   * Gets or sets the client URI.
   */
  @Expose
  public String clientRelayUri;

  /**
   * Gets or sets an array of public keys, which can be used by clients to
   * authenticate the host.
   */
  @Expose
  public String[] hostPublicKeys;
}
