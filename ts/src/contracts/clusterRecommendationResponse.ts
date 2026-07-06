// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendationResponse.cs
/* eslint-disable */

import { ClusterRecommendation } from './clusterRecommendation';

/**
 * Response from the cluster recommendation API containing ranked cluster recommendations.
 */
export interface ClusterRecommendationResponse {
    /**
     * Gets or sets the preferred cluster ID that was requested, if any.
     */
    preferredClusterId?: string;

    /**
     * Gets or sets the recommended cluster ID — the best available cluster. Null if no
     * clusters are available.
     */
    recommendedClusterId?: string;

    /**
     * Gets or sets a value indicating whether the recommendation differs from the
     * preferred cluster.
     */
    isFallback?: boolean;

    /**
     * Gets or sets the ordered list of cluster recommendations, ranked by preference.
     */
    recommendations: ClusterRecommendation[];
}
