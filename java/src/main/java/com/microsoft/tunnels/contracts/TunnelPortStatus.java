// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelPortStatus.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Date;

/**
 * Data contract for {@link TunnelPort} status.
 */
public class TunnelPortStatus {
    /**
     * Gets or sets the current value and limit for the number of clients connected to the
     * port.
     *
     * This client connection count does not include non-port-specific connections such as
     * SDK and SSH clients. See {@link TunnelStatus#clientConnectionCount} for status of
     * those connections.  This count also does not include HTTP client connections,
     * unless they are upgraded to websockets. HTTP connections are counted per-request
     * rather than per-connection: see {@link TunnelPortStatus#httpRequestRate}.
     */
    @Expose
    public ResourceStatus clientConnectionCount;

    /**
     * Gets or sets the UTC date time when a client was last connected to the port, or
     * null if a client has never connected.
     */
    @Expose
    public Date lastClientConnectionTime;

    /**
     * Gets or sets the current value and limit for the rate of client connections to the
     * tunnel port.
     *
     * This client connection rate does not count non-port-specific connections such as
     * SDK and SSH clients. See {@link TunnelStatus#clientConnectionRate} for those
     * connection types.  This also does not include HTTP connections, unless they are
     * upgraded to websockets. HTTP connections are counted per-request rather than
     * per-connection: see {@link TunnelPortStatus#httpRequestRate}.
     */
    @Expose
    public RateStatus clientConnectionRate;

    /**
     * Gets or sets the current value and limit for the rate of HTTP requests to the
     * tunnel port.
     */
    @Expose
    public RateStatus httpRequestRate;
}
