// Generated from ../../../../../../../../cs/src/Contracts/TunnelEndpoint.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.net.URI;

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
    public static final String portUriToken = "{port}";

    /**
     * Gets a URI where a web client can connect to a tunnel port.
     *
     * Requests to the URI may result in HTTP 307 redirections, so the client may need to
     * follow the redirection in order to connect to the port. <para /> If the port is not
     * currently shared via the tunnel, or if a host is not currently connected to the
     * tunnel, then requests to the port URI may result in a 502 Bad Gateway response.
     */
    public static URI getPortUri(TunnelEndpoint endpoint, int portNumber) {
        return TunnelEndpointStatics.getPortUri(endpoint, portNumber);
    }
}
