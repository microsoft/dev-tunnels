// <copyright file="TunnelStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.TunnelService.Contracts;

/// <summary>
/// Data contract for <see cref="Tunnel"/> status.
/// </summary>
public class TunnelStatus
{
    /// <summary>
    /// Gets or sets the current value and limit for the number of ports on the tunnel.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceStatus? PortCount { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the number of hosts currently accepting
    /// connections to the tunnel.
    /// </summary>
    /// <remarks>
    /// This is typically 0 or 1, but may be more than 1 if the tunnel options allow
    /// multiple hosts.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceStatus? HostConnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when a host was last accepting connections to the tunnel,
    /// or null if a host has never connected.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastHostConnectionTime { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the number of clients connected to
    /// the tunnel.
    /// </summary>
    /// <remarks>
    /// This counts non-port-specific client connections, which is SDK and SSH clients.
    /// See <see cref="TunnelPortStatus" /> for status of per-port client connections.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceStatus? ClientConnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when a client last connected to the tunnel, or null if
    /// a client has never connected.
    /// </summary>
    /// <remarks>
    /// This reports times for non-port-specific client connections, which is SDK client and
    /// SSH clients. See <see cref="TunnelPortStatus" /> for per-port client connections.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastClientConnectionTime { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of client connections to the
    /// tunnel.
    /// </summary>
    /// <remarks>
    /// This counts non-port-specific client connections, which is SDK client and SSH clients.
    /// See <see cref="TunnelPortStatus" /> for status of per-port client connections.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? ClientConnectionRate { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of bytes transferred
    /// via the tunnel.
    /// </summary>
    /// <remarks>
    /// This includes both sending and receiving. All types of tunnel and port connections
    /// contribute to this rate.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? DataTransferRate { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of management API read operations 
    /// for the tunnel or tunnel ports.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? ApiReadRate { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of management API update
    /// operations for the tunnel or tunnel ports.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? ApiUpdateRate { get; set; }
}
