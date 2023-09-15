// <copyright file="TunnelListByRegion.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Tunnel list by region.
/// </summary>
public  class TunnelListByRegion
{
    /// <summary>
    /// Azure region name.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegionName { get; set; }

    /// <summary>
    /// Cluster id in the region.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClusterId { get; set; }

    /// <summary>
    /// List of tunnels.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Tunnel[]? Value { get; set; }

    /// <summary>
    /// Error detail if getting list of tunnels in the region failed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetail? Error { get; set; }
}
