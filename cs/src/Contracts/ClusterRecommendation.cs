// <copyright file="ClusterRecommendation.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// A single cluster recommendation with availability and capacity details.
/// </summary>
public class ClusterRecommendation
{
    /// <summary>
    /// Gets or sets the cluster ID, e.g. "usw2".
    /// </summary>
    public string ClusterId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Azure location name, e.g. "WestUs2".
    /// </summary>
    public string AzureLocation { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Azure geography name for data residency, e.g. "United States".
    /// </summary>
    public string AzureGeo { get; set; } = null!;

    /// <summary>
    /// Gets or sets the cluster URI for API requests.
    /// </summary>
    public string ClusterUri { get; set; } = null!;

    /// <summary>
    /// Gets or sets the availability status of the cluster.
    /// </summary>
    public ClusterAvailability Availability { get; set; }

    /// <summary>
    /// Gets or sets the utilization percentage of the cluster.
    /// </summary>
    public double UtilizationPercent { get; set; }

    /// <summary>
    /// Gets or sets a human-readable reason for this recommendation's ranking.
    /// </summary>
    public string Reason { get; set; } = null!;
}
