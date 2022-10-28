// <copyright file="TunnelStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

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
    /// Gets or sets the current value and limit for the rate of bytes being received by the tunnel
    /// host and uploaded by tunnel clients.
    /// </summary>
    /// <remarks>
    /// All types of tunnel and port connections, from potentially multiple clients, can
    /// contribute to this rate. The reported rate may differ slightly from the rate measurable
    /// by applications, due to protocol overhead. Data rate status reporting is delayed by a few
    /// seconds, so this value is a snapshot of the data transfer rate from a few seconds earlier.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? UploadRate { get; set; }

    /// <summary>
    /// Gets or sets the current value and limit for the rate of bytes being sent by the tunnel
    /// host and downloaded by tunnel clients.
    /// </summary>
    /// <remarks>
    /// All types of tunnel and port connections, from potentially multiple clients, can
    /// contribute to this rate. The reported rate may differ slightly from the rate measurable
    /// by applications, due to protocol overhead. Data rate status reporting is delayed by a few
    /// seconds, so this value is a snapshot of the data transfer rate from a few seconds earlier.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RateStatus? DownloadRate { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes received by the tunnel host and uploaded by tunnel
    /// clients, over the lifetime of the tunnel.
    /// </summary>
    /// <remarks>
    /// All types of tunnel and port connections, from potentially multiple clients, can
    /// contribute to this total. The reported value may differ slightly from the value measurable
    /// by applications, due to protocol overhead. Data transfer status reporting is delayed by
    /// a few seconds.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? UploadTotal { get; set; }

    /// <summary>
    /// Gets or sets the total number of bytes sent by the tunnel host and downloaded by tunnel
    /// clients, over the lifetime of the tunnel.
    /// </summary>
    /// <remarks>
    /// All types of tunnel and port connections, from potentially multiple clients, can
    /// contribute to this total. The reported value may differ slightly from the value measurable
    /// by applications, due to protocol overhead. Data transfer status reporting is delayed by
    /// a few seconds.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? DownloadTotal { get; set; }

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
