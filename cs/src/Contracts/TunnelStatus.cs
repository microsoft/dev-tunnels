// <copyright file="TunnelStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for <see cref="Tunnel"/> status.
    /// </summary>
    public class TunnelStatus
    {
        /// <summary>
        /// Gets or sets the number of hosts currently accepting connections to the tunnel.
        /// </summary>
        /// <remarks>
        /// This is typically 0 or 1, but may be more than 1 if the tunnel options allow
        /// multiple hosts.
        /// </remarks>
        public uint HostConnectionCount { get; set; }

        /// <summary>
        /// Gets or sets the UTC time when a host was last accepting connections to the tunnel,
        /// or null if a host has never connected.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastHostConnectionTime { get; set; }

        /// <summary>
        /// Gets or sets the number of clients currently connected to the tunnel.
        /// </summary>
        public uint ClientConnectionCount { get; set; }

        /// <summary>
        /// Gets or sets the UTC time when a client last connected to the tunnel, or null if
        /// a client has never connected.
        /// </summary>
        public DateTime? LastClientConnectionTime { get; set; }
    }
}
