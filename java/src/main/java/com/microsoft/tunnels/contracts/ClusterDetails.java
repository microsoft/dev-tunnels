// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ClusterDetails.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Tunnel service cluster details.
 */
public class ClusterDetails {
    /**
     * A cluster identifier based on its region.
     */
    @Expose
    public String clusterId;

    /**
     * The cluster DNS host.
     */
    @Expose
    public String host;
}
