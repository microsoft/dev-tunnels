// <copyright file="TunnelRelayTunnelEndpoint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
/// </summary>
public class TunnelRelayTunnelEndpoint : TunnelEndpoint
{
    // TODO: Add validation attributes on properties of this class.

    /// <summary>
    /// Initializes a new instance of the <see cref="TunnelRelayTunnelEndpoint"/> class.
    /// </summary>
    public TunnelRelayTunnelEndpoint()
    {
        ConnectionMode = TunnelConnectionMode.TunnelRelay;
    }

    /// <summary>
    /// Gets or sets the host URI.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HostRelayUri { get; set; }

    /// <summary>
    /// Gets or sets the client URI.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientRelayUri { get; set; }
}
