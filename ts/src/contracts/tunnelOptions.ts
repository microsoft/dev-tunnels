/**
 * Data contract for Tunnel or TunnelPort options.
 */
export interface TunnelOptions {
    /**
     * Gets or sets a value indicating whether web-forwarding of this tunnel can run on any cluster
     * without redirecting to the home cluster.
     * This is only applicable if the tunnel has a name and web-forwarding uses it.
     */
    isGlobalAccess?: boolean;
}
