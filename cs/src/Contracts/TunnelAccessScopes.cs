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
        /// Allows relay service principle to access tunnels.
        /// </summary>
        public const string RelayRole = "relay";

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

        /// <summary>
        /// Checks that all items in an array of scopes are valid.
        /// </summary>
        /// <exception cref="ArgumentException">A scope is not valid.</exception>
        public static void Validate(
            IEnumerable<string> scopes,
            IEnumerable<string>? validScopes = null)
        {
            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            foreach (var scope in scopes)
            {
                if (string.IsNullOrEmpty(scope))
                {
                    throw new ArgumentException(
                        $"Tunnel access scopes include a null/empty item.", nameof(scopes));
                }
                else if (!TunnelAccessScopes.All.Contains(scope))
                {
                    throw new ArgumentException(
                        $"Invalid tunnel access scope: {scope}", nameof(scopes));
                }
            }

            if (validScopes != null)
            {
                foreach (var scope in scopes)
                {
                    if (!validScopes.Contains(scope))
                    {
                        throw new ArgumentException(
                            $"Tunnel access scope is invalid for current request: {scope}",
                            nameof(scopes));
                    }
                }
            }
        }
    }
}
