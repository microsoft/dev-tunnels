// <copyright file="TunnelListResponse.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Data contract for response of a list tunnel call.
/// </summary>
public class TunnelListResponse
{

    /// <summary>
    /// Initializes a new instance of the <see cref="Tunnel"/> class.
    /// </summary>
    public TunnelListResponse()
    {
        Value = Array.Empty<TunnelV2>();
    }

    /// <summary>
    /// List of tunnels
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelV2[] Value { get; set; }

    /// <summary>
    /// Link to get next page of results
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextLink { get; set; }

}
