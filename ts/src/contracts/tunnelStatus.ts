// Generated from ../../../cs/src/Contracts/TunnelStatus.cs

/**
 * Data contract for {@link Tunnel} status.
 */
export interface TunnelStatus {
    /**
     * Gets or sets the number of hosts currently accepting connections to the tunnel.
     *
     * This is typically 0 or 1, but may be more than 1 if the tunnel options allow
     * multiple hosts.
     */
    hostConnectionCount: number;

    /**
     * Gets or sets the UTC time when a host was last accepting connections to the tunnel,
     * or null if a host has never connected.
     */
    lastHostConnectionTime?: Date;

    /**
     * Gets or sets the number of clients currently connected to the tunnel.
     */
    clientConnectionCount: number;

    /**
     * Gets or sets the UTC time when a client last connected to the tunnel, or null if a
     * client has never connected.
     */
    lastClientConnectionTime?: Date;
}
