// <copyright file="TunnelSshKeyResponse.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Response for SshKey endpoint.
/// </summary>
public class TunnelSshKeyResponse
{
    /// <summary>
    /// Gets or sets the ssh key for a tunnel.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SshKey { get; set; }
}
