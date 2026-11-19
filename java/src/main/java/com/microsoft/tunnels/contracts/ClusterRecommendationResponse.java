// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ClusterRecommendationResponse.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Response from the cluster recommendation API containing ranked cluster recommendations.
 */
public class ClusterRecommendationResponse {
    /**
     * Gets or sets the preferred cluster ID that was requested, if any.
     */
    @Expose
    public String preferredClusterId;

    /**
     * Gets or sets the recommended cluster ID — the best available cluster. Null if no
     * clusters are available.
     */
    @Expose
    public String recommendedClusterId;

    /**
     * Gets or sets a value indicating whether the recommendation differs from the
     * preferred cluster.
     */
    @Expose
    public boolean isFallback;

    /**
     * Gets or sets the ordered list of cluster recommendations, ranked by preference.
     */
    @Expose
    public ClusterRecommendation[] recommendations;
}
