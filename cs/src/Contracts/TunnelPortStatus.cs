// <copyright file="TunnelPortStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Data contract for <see cref="TunnelPort"/> status.
/// </summary>
public class TunnelPortStatus
{
    /// <summary>
    /// Gets or sets the current value and limit for the number of clients connected to
    /// the port.
    /// </summary>
    /// <remarks>
    /// This client connection count does not include non-port-specific connections such
    /// as SDK and SSH clients. See <see cref="TunnelStatus.ClientConnectionCount"/> for
    /// status of those connections.
    /// 
    /// This count also does not include HTTP client connections, unless they are upgraded
    /// to websockets. HTTP connections are counted per-request rather than per-connection:
    /// see <see cref="HttpRequestRate"/>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceStatus? ClientConnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC date time when a client was last connected to the port, or null
    /// if a client has never connected.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastClientConnectionTime { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of client connections to the
    /// tunnel port.
    /// </summary>
    /// <remarks>
    /// This client connection rate does not count non-port-specific connections such
    /// as SDK and SSH clients. See <see cref="TunnelStatus.ClientConnectionRate"/> for
    /// those connection types.
    ///
    /// This also does not include HTTP connections, unless they are upgraded to websockets.
    /// HTTP connections are counted per-request rather than per-connection: see
    /// <see cref="HttpRequestRate"/>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? ClientConnectionRate { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of HTTP requests to the tunnel
    /// port.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? HttpRequestRate { get; set; }
}
