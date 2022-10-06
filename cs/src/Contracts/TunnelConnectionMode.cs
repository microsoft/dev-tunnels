// <copyright file="TunnelConnectionMode.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Specifies the connection protocol / implementation for a tunnel.
    /// </summary>
    /// <remarks>
    /// Depending on the connection mode, hosts or clients might need to use different
    /// authentication and connection protocols.
    /// </remarks>
    public enum TunnelConnectionMode
    {
        /// <summary>
        /// Connect directly to the host over the local network.
        /// </summary>
        /// <remarks>
        /// While it's technically not "tunneling", this mode may be combined
        /// with others to enable choosing the most efficient connection mode available.
        /// </remarks>
        LocalNetwork = 0,

        /// <summary>
        /// Use the tunnel service's integrated relay function.
        /// </summary>
        TunnelRelay = 1,
    }
}
