// <copyright file="TunnelAccessScopes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Defines scopes for tunnel access tokens.
    /// </summary>
    public static class TunnelAccessScopes
    {
        /// <summary>
        /// Allows management operations on tunnels and tunnel ports.
        /// </summary>
        public const string Manage = "manage";

        /// <summary>
        /// Allows accepting connections on tunnels as a host.
        /// </summary>
        public const string Host = "host";

        /// <summary>
        /// Allows inspecting tunnel connection activity and data.
        /// </summary>
        public const string Inspect = "inspect";

        /// <summary>
        /// Allows connecting to tunnels as a client.
        /// </summary>
        public const string Connect = "connect";

        /// <summary>
        /// Array of all access scopes.
        /// </summary>
        public static readonly string[] All = new[]
        {
            Manage,
            Host,
            Inspect,
            Connect,
        };
    }
}
