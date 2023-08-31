// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelV1.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Tunnel type used for tunnel service API version 2023-05-23-preview
 */
public class TunnelV extends TunnelBase {
    /**
     * Gets or sets the ID of the tunnel, unique within the cluster.
     */
    @Expose
    public String tunnelId;

    /**
     * Gets or sets the tags of the tunnel.
     */
    @Expose
    public String[] tags;
}
