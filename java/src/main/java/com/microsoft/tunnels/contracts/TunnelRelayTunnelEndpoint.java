// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
 */
public class TunnelRelayTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets the host URI.
     */
    @Expose
    public String hostRelayUri;

    /**
     * Gets or sets the client URI.
     */
    @Expose
    public String clientRelayUri;
}
