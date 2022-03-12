/**
 * Specifies the connection protocol / implementation for a tunnel.
 */
export enum TunnelConnectionMode {
    /**
     * Connect directly to the host over the local network.
     */
    LocalNetwork = 'LocalNetwork',

    /**
     * Use the tunnel service's integrated relay function.
     */
    TunnelRelay = 'TunnelRelay',

    /**
     * Connect via a Live Share workspace's Azure Relay endpoint.
     */
    LiveShareRelay = 'LiveShareRelay',
}
