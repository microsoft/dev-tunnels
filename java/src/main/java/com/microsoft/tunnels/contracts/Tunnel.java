// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/Tunnel.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Date;
import java.util.Map;

/**
 * Data contract for tunnel objects managed through the tunnel service REST API.
 */
public class Tunnel {
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
     * Gets or sets the optional short name (alias) of the tunnel.
     *
     * The name must be globally unique within the parent domain, and must be a valid
     * subdomain.
     */
    @Expose
    public String name;

    /**
     * Gets or sets the description of the tunnel.
     */
    @Expose
    public String description;

    /**
     * Gets or sets the labels of the tunnel.
     */
    @Expose
    public String[] labels;

    /**
     * Gets or sets the optional parent domain of the tunnel, if it is not using the
     * default parent domain.
     */
    @Expose
    public String domain;

    /**
     * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
     */
    @Expose
    public Map<String, String> accessTokens;

    /**
     * Gets or sets access control settings for the tunnel.
     *
     * See {@link TunnelAccessControl} documentation for details about the access control
     * model.
     */
    @Expose
    public TunnelAccessControl accessControl;

    /**
     * Gets or sets default options for the tunnel.
     */
    @Expose
    public TunnelOptions options;

    /**
     * Gets or sets current connection status of the tunnel.
     */
    @Expose
    public TunnelStatus status;

    /**
     * Gets or sets an array of endpoints where hosts are currently accepting client
     * connections to the tunnel.
     */
    @Expose
    public TunnelEndpoint[] endpoints;

    /**
     * Gets or sets a list of ports in the tunnel.
     *
     * This optional property enables getting info about all ports in a tunnel at the same
     * time as getting tunnel info, or creating one or more ports at the same time as
     * creating a tunnel. It is omitted when listing (multiple) tunnels, or when updating
     * tunnel properties. (For the latter, use APIs to create/update/delete individual
     * ports instead.)
     */
    @Expose
    public TunnelPort[] ports;

    /**
     * Gets or sets the time in UTC of tunnel creation.
     */
    @Expose
    public Date created;

    /**
     * Gets or the time the tunnel will be deleted if it is not used or updated.
     */
    @Expose
    public Date expiration;

    /**
     * Gets or the custom amount of time the tunnel will be valid if it is not used or
     * updated in seconds.
     */
    @Expose
    public int customExpiration;
}
