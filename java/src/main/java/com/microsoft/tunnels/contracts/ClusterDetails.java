// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ClusterDetails.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Details of a tunneling service cluster. Each cluster represents an instance of the
 * tunneling service running in a particular Azure region. New tunnels are created in the
 * current region unless otherwise specified.
 */
public class ClusterDetails {
    ClusterDetails (String clusterId, String uri, String azureLocation) {
        this.clusterId = clusterId;
        this.uri = uri;
        this.azureLocation = azureLocation;
    }

    /**
     * A cluster identifier based on its region.
     */
    @Expose
    public final String clusterId;

    /**
     * The URI of the service cluster.
     */
    @Expose
    public final String uri;

    /**
     * The Azure location of the cluster.
     */
    @Expose
    public final String azureLocation;
}
