package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

public class TunnelEndpoint {
    /**
     * Gets or sets the connection mode of the endpoint.
     * This property is required when creating or updating an endpoint.
     */
    @Expose
    public TunnelConnectionMode connectionMode;

    /**
     * Gets or sets the ID of the host that is listening on this endpoint.
     * This property is required when creating or updating an endpoint.
     */
    @Expose
    public String hostId;

    /**
     * Gets or sets a string used to format URIs where a web client can connect to
     * ports of the tunnel. The string includes a `{port}` that must be
     * replaced with the actual port number.
     */
    @Expose
    public String portUriFormat;
}
