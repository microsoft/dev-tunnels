// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ClusterRecommendation.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * A single cluster recommendation with availability and capacity details.
 */
public class ClusterRecommendation {
    /**
     * Gets or sets the cluster ID, e.g. "usw2".
     */
    @Expose
    public String clusterId;

    /**
     * Gets or sets the Azure location name, e.g. "WestUs2".
     */
    @Expose
    public String azureLocation;

    /**
     * Gets or sets the Azure geography name for data residency, e.g. "United States".
     */
    @Expose
    public String azureGeo;

    /**
     * Gets or sets the cluster URI for API requests.
     */
    @Expose
    public String clusterUri;

    /**
     * Gets or sets the availability status of the cluster.
     */
    @Expose
    public ClusterAvailability availability;

    /**
     * Gets or sets the utilization percentage of the cluster.
     */
    @Expose
    public double utilizationPercent;

    /**
     * Gets or sets a human-readable reason for this recommendation's ranking.
     */
    @Expose
    public String reason;
}
