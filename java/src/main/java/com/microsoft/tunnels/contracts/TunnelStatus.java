// Generated from ../../../../../../../../cs/src/Contracts/TunnelStatus.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Data contract for {@link Tunnel} status.
 */
public class TunnelStatus {
    /**
     * Gets or sets the number of hosts currently accepting connections to the tunnel.
     *
     * This is typically 0 or 1, but may be more than 1 if the tunnel options allow
     * multiple hosts.
     */
    @Expose
    public int hostConnectionCount;

    /**
     * Gets or sets the UTC time when a host was last accepting connections to the tunnel,
     * or null if a host has never connected.
     */
    @Expose
    public java.util.Date lastHostConnectionTime;

    /**
     * Gets or sets the number of clients currently connected to the tunnel.
     */
    @Expose
    public int clientConnectionCount;

    /**
     * Gets or sets the UTC time when a client last connected to the tunnel, or null if a
     * client has never connected.
     */
    @Expose
    public java.util.Date lastClientConnectionTime;
}
