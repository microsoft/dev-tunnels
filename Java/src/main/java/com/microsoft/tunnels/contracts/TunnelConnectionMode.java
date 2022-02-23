package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

/**
 * TunnelConnectionMode.
 */
public enum TunnelConnectionMode {
  /**
   * Connect directly to the host over the local network.
   */
  @SerializedName("LocalNetwork")
  LocalNetwork,

  /**
   * Use the tunnel service's integrated relay function.
   */
  @SerializedName("TunnelRelay")
  TunnelRelay,

  /**
   * Connect via a Live Share workspace's Azure Relay endpoint.
   */
  @SerializedName("LiveShareRelay")
  LiveShareRelay
}
