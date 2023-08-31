// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/Tunnel.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Tunnel type used for tunnel service API versions greater than 2023-05-23-preview
 */
public class TunnelV2 extends TunnelBase {
    /**
     * Gets or sets the ID of the tunnel, unique within the cluster.
     */
    @Expose
    public String tunnelId;

    /**
     * Gets or sets the tags of the tunnel.
     */
    @Expose
    public String[] labels;
}
