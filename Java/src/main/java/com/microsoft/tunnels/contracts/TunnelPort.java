package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.HashMap;

/**
 * Data contract for tunnel port objects managed through the tunnel service REST
 * API.
 */
public class TunnelPort {
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
   * Gets or sets the IP port number of the tunnel port.
   */
  @Expose
  public int portNumber;

  /**
   * Gets or sets the protocol of the tunnel port.
   */
  @Expose
  public String protocol;

  /**
   * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
   */
  @Expose
  public HashMap<String, String> accessTokens;

  /**
  * Gets or sets access control settings for the tunnel port.
  */
  @Expose
  public TunnelAccessControl accessControl;

  /**
   * Gets or sets options for the tunnel port.
   */
  @Expose
  public TunnelOptions options;

  /**
   * Gets or sets current connection status of the tunnel port.
   */
  @Expose
  public TunnelStatus status;

  public TunnelPort() {
  };
}
