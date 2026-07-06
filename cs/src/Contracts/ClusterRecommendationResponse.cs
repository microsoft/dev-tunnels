// <copyright file="ClusterRecommendationResponse.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Response from the cluster recommendation API containing ranked cluster recommendations.
/// </summary>
public class ClusterRecommendationResponse
{
    /// <summary>
    /// Gets or sets the preferred cluster ID that was requested, if any.
    /// </summary>
    public string? PreferredClusterId { get; set; }

    /// <summary>
    /// Gets or sets the recommended cluster ID — the best available cluster.
    /// Null if no clusters are available.
    /// </summary>
    public string? RecommendedClusterId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recommendation differs
    /// from the preferred cluster.
    /// </summary>
    public bool IsFallback { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of cluster recommendations, ranked by preference.
    /// </summary>
    public ClusterRecommendation[] Recommendations { get; set; }
        = Array.Empty<ClusterRecommendation>();
}
