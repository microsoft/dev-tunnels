// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelPort.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Map;

/**
 * Data contract for tunnel port objects managed through the tunnel service REST API.
 */
public class TunnelPort {
    /**
     * Gets or sets the ID of the cluster the tunnel was created in.
     */
    @Expose
    public String clusterId;

    /**
     * Gets or sets the generated ID of the tunnel, unique within the cluster.
     */
    @Expose
    public String tunnelId;

    /**
     * Gets or sets the IP port number of the tunnel port.
     */
    @Expose
    public int portNumber;

    /**
     * Gets or sets the protocol of the tunnel port.
     *
     * Should be one of the string constants from {@link TunnelProtocol}.
     */
    @Expose
    public String protocol;

    /**
     * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
     *
     * Unlike the tokens in {@link Tunnel#accessTokens}, these tokens are restricted to
     * the individual port.
     */
    @Expose
    public Map<String, String> accessTokens;

    /**
     * Gets or sets access control settings for the tunnel port.
     *
     * See {@link TunnelAccessControl} documentation for details about the access control
     * model.
     */
    @Expose
    public TunnelAccessControl accessControl;

    /**
     * Gets or sets options for the tunnel port.
     */
    @Expose
    public TunnelOptions options;

    /**
     * Gets or sets current connection status of the tunnel port.
     */
    @Expose
    public TunnelPortStatus status;
}
