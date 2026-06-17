// <copyright file="ClusterAvailability.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Availability status of a tunneling service cluster.
/// </summary>
public enum ClusterAvailability
{
    /// <summary>
    /// Cluster has sufficient capacity and is fully available.
    /// </summary>
    Available,

    /// <summary>
    /// Cluster is approaching capacity limits and may experience delays.
    /// </summary>
    Degraded,

    /// <summary>
    /// Cluster is at or beyond capacity and should not be used for new tunnels.
    /// </summary>
    Unavailable,
}
