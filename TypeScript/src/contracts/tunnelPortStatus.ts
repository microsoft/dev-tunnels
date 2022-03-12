/**
 * Data contract for TunnelPort status.
 */
export interface TunnelPortStatus {
    /**
     * Gets or sets the number of clients currently connected to the port.
     */
    clientConnectionCount?: number;

    /**
     * Gets or sets the UTC date time when a client was last connected to the port.
     */
    lastClientConnectionTime?: Date;
}
