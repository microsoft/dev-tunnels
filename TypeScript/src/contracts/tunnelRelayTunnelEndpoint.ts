import { TunnelEndpoint } from './tunnelEndpoint';

/**
 * Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
 */
export interface TunnelRelayTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets the host URI.
     */
    hostRelayUri?: string;

    /**
     * Gets or sets the client URI.
     */
    clientRelayUri?: string;

    /**
     * Gets or sets an array of public keys, which can be used by clients to authenticate the host.
     */
    hostPublicKeys?: string[];
}
