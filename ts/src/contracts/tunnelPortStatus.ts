// Generated from ../../../cs/src/Contracts/TunnelPortStatus.cs

/**
 * Data contract for {@link TunnelPort} status.
 */
export interface TunnelPortStatus {
    /**
     * Gets or sets the number of clients currently connected to the port.
     *
     * The client connection count does not include the host. (See the {@link
     * TunnelStatus.hostConnectionCount} property for host connection status. Hosts always
     * listen for incoming connections on all tunnel ports simultaneously.)
     */
    clientConnectionCount: number;

    /**
     * Gets or sets the UTC date time when a client was last connected to the port.
     */
    lastClientConnectionTime?: Date;
}
