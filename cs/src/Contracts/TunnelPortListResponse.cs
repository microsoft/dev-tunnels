// <copyright file="TunnelPortListResponse.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Data contract for response of a list tunnel ports call.
/// </summary>
public class TunnelPortListResponse
{

    /// <summary>
    /// Initializes a new instance of the <see cref="TunnelPortListResponse"/> class.
    /// </summary>
    public TunnelPortListResponse()
    {
        Value = Array.Empty<TunnelPort>();
    }

    /// <summary>
    /// List of tunnels
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelPort[] Value { get; set; }

    /// <summary>
    /// Link to get next page of results
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextLink { get; set; }

}
