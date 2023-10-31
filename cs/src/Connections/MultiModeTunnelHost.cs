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
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh.Tcp.Events;

namespace Microsoft.DevTunnels.Connections
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
        public bool ForwardConnectionsToLocalPorts
        {
            get => Hosts.Any(c => c.ForwardConnectionsToLocalPorts);
            set
            {
                foreach (var host in Hosts)
                {
                    host.ForwardConnectionsToLocalPorts = value;
                }
            }
        }

#pragma warning disable CS0067 // Not used
        /// <inheritdoc />
        public event EventHandler<ForwardedPortConnectingEventArgs>? ForwardedPortConnecting;
#pragma warning restore CS0067

        /// <inheritdoc />
        public override async Task ConnectAsync(
            Tunnel tunnel,
            TunnelConnectionOptions? options,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(tunnel, nameof(tunnel));

            var startTasks = new List<Task>();

            foreach (var host in Hosts)
            {
                startTasks.Add(host.ConnectAsync(tunnel, options, cancellation));
            }

            await Task.WhenAll(startTasks);
        }

        /// <inheritdoc />
        protected override async Task DisposeConnectionAsync()
        {
            await base.DisposeConnectionAsync();

            var disposeTasks = new List<Task>();

            foreach (var host in Hosts)
            {
                disposeTasks.Add(host.DisposeAsync().AsTask());
            }

            await Task.WhenAll(disposeTasks);
        }

        /// <inheritdoc />
        public async Task RefreshPortsAsync(CancellationToken cancellation)
        {
            await Task.WhenAll(
                Hosts.Select((c) => c.RefreshPortsAsync(cancellation)));
        }
    }
}
