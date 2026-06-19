// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ClusterAvailability.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

/**
 * Availability status of a tunneling service cluster.
 */
public enum ClusterAvailability {
    /**
     * Cluster has sufficient capacity and is fully available.
     */
    @SerializedName("Available")
    Available,

    /**
     * Cluster is approaching capacity limits and may experience delays.
     */
    @SerializedName("Degraded")
    Degraded,

    /**
     * Cluster is at or beyond capacity and should not be used for new tunnels.
     */
    @SerializedName("Unavailable")
    Unavailable,
}
