// <copyright file="TunnelPortStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for <see cref="TunnelPort"/> status.
    /// </summary>
    public class TunnelPortStatus
    {
        /// <summary>
        /// Gets or sets the number of clients currently connected to the port.
        /// </summary>
        /// <remarks>
        /// The client connection count does not include the host. (See the
        /// <see cref="TunnelStatus.HostConnectionCount" /> property for host connection status.
        /// Hosts always listen for incoming connections on all tunnel ports simultaneously.)
        /// </remarks>
        public uint ClientConnectionCount { get; set; }

        /// <summary>
        /// Gets or sets the UTC date time when a client was last connected to the port.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastClientConnectionTime { get; set; }
    }
}
