// <copyright file="LiveShareRelayTunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;
using Microsoft.VisualStudio.Ssh;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Tunnel client implementation that connects via a Live Share session's Azure Relay.
    /// </summary>
    public class LiveShareRelayTunnelClient : TunnelClientBase
    {
        /// <summary>
        /// Creates a new instance of a client that connects to a tunnel via a Live Share
        /// session's Azure Relay.
        /// </summary>
        public LiveShareRelayTunnelClient(
            ITunnelManagementClient managementClient,
            TraceSource trace) : base(trace)
        {
            ManagementClient = Requires.NotNull(managementClient, nameof(managementClient));
        }

        private ITunnelManagementClient ManagementClient { get; }

        /// <inheritdoc />
        public override IReadOnlyCollection<TunnelConnectionMode> ConnectionModes
            => new[] { TunnelConnectionMode.LiveShareRelay };

        /// <inheritdoc />
        protected override async Task ConnectAsync(Tunnel tunnel, IEnumerable<TunnelEndpoint> endpoints, CancellationToken cancellation)
        {
            var liveShareEndpoint = endpoints
                .OfType<LiveShareRelayTunnelEndpoint>()
                .SingleOrDefault() ??
                throw new InvalidOperationException(
                    "The host is not currently accepting Live Share relay connections.");

            var relayUri = liveShareEndpoint.RelayUri;
            if (string.IsNullOrEmpty(relayUri))
            {
                throw new InvalidOperationException(
                    "The Live Share relay endpoint URI is missing.");
            }

            var tokenProvider = new LiveShareRelayTokenProvider(
                ManagementClient,
                tunnel,
                liveShareEndpoint.HostId!,
                TunnelAccessScopes.Connect,
                liveShareEndpoint.RelayClientSasToken);
            var relayClient = new HybridConnectionClient(new Uri(relayUri), tokenProvider);

            HybridConnectionStream stream;
            try
            {
                stream = await relayClient.CreateConnectionAsync();
            }
            catch (RelayException rex)
            {
                throw new TunnelConnectionException("Failed to connect to tunnel relay.", rex);
            }

            try
            {
                await StartSshSessionAsync(stream, cancellation);
            }
            catch (SshConnectionException scex)
            {
                throw new TunnelConnectionException("Failed to secure tunnel connection.", scex);
            }
        }
    }
}
