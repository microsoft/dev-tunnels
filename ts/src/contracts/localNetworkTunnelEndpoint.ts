// Generated from ../../../cs/src/Contracts/LocalNetworkTunnelEndpoint.cs

import { TunnelEndpoint } from './tunnelEndpoint';

/**
 * Parameters for connecting to a tunnel via a local network connection.
 *
 * While a direct connection is technically not "tunneling", tunnel hosts may accept
 * connections via the local network as an optional more-efficient alternative to a relay.
 */
export interface LocalNetworkTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets a list of IP endpoints where the host may accept connections.
     *
     * A host may accept connections on multiple IP endpoints simultaneously if there are
     * multiple network interfaces on the host system and/or if the host supports both
     * IPv4 and IPv6.  Each item in the list is a URI consisting of a scheme (which gives
     * an indication of the network connection protocol), an IP address (IPv4 or IPv6) and
     * a port number. The URIs do not typically include any paths, because the connection
     * is not normally HTTP-based.
     */
    hostEndpoints: string[];
}