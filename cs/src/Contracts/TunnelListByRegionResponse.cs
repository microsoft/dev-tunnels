// <copyright file="TunnelListResponse.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Data contract for response of a list tunnel by region call.
/// </summary>
public class TunnelListByRegionResponse
{
    /// <summary>
    /// List of tunnels
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelListByRegion[]? Value { get; set; }

    /// <summary>
    /// Link to get next page of results.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextLink { get; set; }
}
