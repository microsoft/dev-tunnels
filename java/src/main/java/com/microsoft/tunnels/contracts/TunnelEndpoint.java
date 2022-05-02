// Generated from ../../../../../../../../cs/src/Contracts/TunnelEndpoint.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Base class for tunnel connection parameters.
 *
 * A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
 * There is a subclass for each connection mode, each having different connection
 * parameters. A tunnel may have multiple endpoints for one host (or multiple hosts), and
 * clients can select their preferred endpoint(s) from those depending on network
 * environment or client capabilities.
 */
public class TunnelEndpoint {
    /**
     * Gets or sets the connection mode of the endpoint.
     *
     * This property is required when creating or updating an endpoint.  The subclass type
     * is also an indication of the connection mode, but this property is necessary to
     * determine the subclass type when deserializing.
     */
    @Expose
    public TunnelConnectionMode connectionMode;

    /**
     * Gets or sets the ID of the host that is listening on this endpoint.
     *
     * This property is required when creating or updating an endpoint.  If the host
     * supports multiple connection modes, the host's ID is the same for all the endpoints
     * it supports. However different hosts may simultaneously accept connections at
     * different endpoints for the same tunnel, if enabled in tunnel options.
     */
    @Expose
    public String hostId;

    /**
     * Gets or sets an array of public keys, which can be used by clients to authenticate
     * the host.
     */
    @Expose
    public String[] hostPublicKeys;

    /**
     * Gets or sets a string used to format URIs where a web client can connect to ports
     * of the tunnel. The string includes a {@link TunnelEndpoint#portUriToken} that must
     * be replaced with the actual port number.
     */
    @Expose
    public String portUriFormat;

    /**
     * Token included in {@link TunnelEndpoint#portUriFormat} that is to be replaced by a
     * specified port number.
     */
    @Expose
    public static String portUriToken = "{port}";
}
