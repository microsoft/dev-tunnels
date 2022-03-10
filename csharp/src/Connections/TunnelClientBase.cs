// <copyright file="TunnelClientBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh.Tcp;
using Microsoft.VisualStudio.Ssh.Tcp.Events;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Base class for clients that connect to a single host
    /// </summary>
    public abstract class TunnelClientBase : SshPfsClientBase, ITunnelClient
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TunnelClientBase" /> class.
        /// </summary>
        public TunnelClientBase(TraceSource trace) : base(trace)
        {
        }

        /// <inheritdoc />
        public abstract IReadOnlyCollection<TunnelConnectionMode> ConnectionModes { get; }

        /// <inheritdoc />
        public ForwardedPortsCollection? ForwardedPorts =>
            SshPortForwardingService?.RemoteForwardedPorts;

        /// <summary>
        /// Do work specific to the type of tunnel client.
        /// </summary>
        protected abstract Task ConnectAsync(Tunnel tunnel, IEnumerable<TunnelEndpoint> endpoints, CancellationToken cancellation);

        /// <inheritdoc />
        public async Task ConnectAsync(Tunnel tunnel, string? hostId, CancellationToken cancellation)
        {
            Requires.NotNull(tunnel, nameof(tunnel));
            Requires.NotNull(tunnel.Endpoints!, nameof(Tunnel.Endpoints));

            if (this.SshSession != null)
            {
                throw new InvalidOperationException(
                    "Already connected. Use separate instances to connect to multiple tunnels.");
            }

            if (tunnel.Endpoints.Length == 0)
            {
                throw new InvalidOperationException(
                    "No hosts are currently accepting connections for the tunnel.");
            }

            var endpointGroups = tunnel.Endpoints.GroupBy((ep) => ep.HostId).ToArray();
            IGrouping<string?, TunnelEndpoint> endpointGroup;
            if (hostId != null)
            {
                endpointGroup = endpointGroups.SingleOrDefault((g) => g.Key == hostId) ??
                    throw new InvalidOperationException(
                        "The specified host is not currently accepting connections to the tunnel.");
            }
            else if (endpointGroups.Length > 1)
            {
                throw new InvalidOperationException(
                    "There are multiple hosts for the tunnel. Specify a host ID to connect to.");
            }
            else
            {
                endpointGroup = endpointGroups.Single();
            }

            await ConnectAsync(tunnel, endpointGroup, cancellation);
        }

        /// <inheritdoc />
        public Task WaitForForwardedPortAsync(int forwardedPort, CancellationToken cancellation) =>
            SshPortForwardingService is PortForwardingService pfs ?
            pfs.WaitForForwardedPortAsync(forwardedPort, cancellation) :
            throw new InvalidOperationException("Port forwarding has not been started. Ensure that the client has connected by calling ConnectAsync.");
    }
}
