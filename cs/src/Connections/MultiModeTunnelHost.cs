// <copyright file="MultiModeTunnelHost.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Aggregation of multiple tunnel hosts.
    /// </summary>
    public class MultiModeTunnelHost : TunnelConnection, ITunnelHost
    {
        /// <summary>
        /// Gets or sets a host ID. An initial value is automatically generated for the process.
        /// </summary>
        /// <remarks>
        /// The host ID uniquely identifies one host process that is accepting connections on a
        /// tunnel. If the host supports multiple connection modes, the host's ID is the same for
        /// all the endpoints it supports.
        /// </remarks>
        public static string HostId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Creates a new instance of the <see cref="MultiModeTunnelHost" /> class
        /// that can simultaneously run multiple single-mode hosts.
        /// </summary>
        public MultiModeTunnelHost(IEnumerable<ITunnelHost> hosts, ITunnelManagementClient managementClient, TraceSource trace) : base(managementClient, trace)
        {
            Hosts = new List<ITunnelHost>(Requires.NotNull(hosts, nameof(hosts)));
            // TODO: Subscribe to hosts' RefreshingTunnelAccessToken event and call TunnelBase.RefreshTunnelAccessTokenAsync() to get the tunnel access token.
        }

        /// <summary>
        /// Gets the list of hosts that can accept connections on the tunnel.
        /// </summary>
        public IEnumerable<ITunnelHost> Hosts { get; }

        /// <inheritdoc />
        protected override string TunnelAccessScope => TunnelAccessScopes.Host;

        /// <inheritdoc />
        public async Task StartAsync(
            Tunnel tunnel,
            CancellationToken cancellation)
        {
            Requires.NotNull(tunnel, nameof(tunnel));

            var startTasks = new List<Task>();

            foreach (var host in Hosts)
            {
                startTasks.Add(host.StartAsync(tunnel, cancellation));
            }

            await Task.WhenAll(startTasks);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();

            var disposeTasks = new List<Task>();

            foreach (var host in Hosts)
            {
                disposeTasks.Add(host.DisposeAsync().AsTask());
            }

            await Task.WhenAll(disposeTasks);
        }

        /// <inheritdoc />
        protected override Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task RefreshPortsAsync(CancellationToken cancellation)
        {
            await Task.WhenAll(
                Hosts.Select((c) => c.RefreshPortsAsync(cancellation)));
        }
    }
}
