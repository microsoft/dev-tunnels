// <copyright file="TunnelOptions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for <see cref="Tunnel"/> or <see cref="TunnelPort"/> options.
    /// </summary>
    public class TunnelOptions
    {
        // TODO: Consider adding an option to enable multiple hosts for a tunnel.
        // The system supports it, but it would only be used in advanced scenarios,
        // and otherwise could cause confusion in case of mistakes.
        // When not enabled, an existing host should be disconnected when a new host connects.

        /// <summary>
        /// Gets or sets a value indicating whether web-forwarding of this tunnel can run on any cluster (region)
        /// without redirecting to the home cluster.
        /// This is only applicable if the tunnel has a name and web-forwarding uses it.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsGloballyAvailable { get; set; }
    }
}
