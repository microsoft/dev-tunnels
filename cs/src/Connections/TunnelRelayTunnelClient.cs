// <copyright file="TunnelRelayTunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
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
    /// Tunnel client implementation that connects via a tunnel relay.
    /// </summary>
    public class TunnelRelayTunnelClient : TunnelClientBase
    {
        /// <summary>
        /// Web socket sub-protocol to connect to the tunnel relay endpoint.
        /// </summary>
        public const string WebSocketSubProtocol = "tunnel-relay-client";

        /// <summary>
        /// Creates a new instance of a client that connects to a tunnel via a tunnel relay.
        /// </summary>
        public TunnelRelayTunnelClient(TraceSource trace) : base(trace) { }

        /// <summary>
        /// Gets or sets a factory for creating relay streams.
        /// </summary>
        /// <remarks>
        /// Normally the default <see cref="TunnelRelayStreamFactory" /> can be used. However a
        /// different factory class may be used to customize the connection (or mock the connection
        /// for testing).
        /// </remarks>
        public ITunnelRelayStreamFactory StreamFactory { get; set; } = new TunnelRelayStreamFactory();

        /// <inheritdoc />
        public override IReadOnlyCollection<TunnelConnectionMode> ConnectionModes
             => new[] { TunnelConnectionMode.TunnelRelay };

        /// <inheritdoc />
        protected override async Task ConnectAsync(
            Tunnel tunnel,
            IEnumerable<TunnelEndpoint> endpoints,
            CancellationToken cancellation)
        {
            var endpoint = endpoints
                .OfType<TunnelRelayTunnelEndpoint>()
                .SingleOrDefault() ??
                throw new InvalidOperationException(
                    "The host is not currently accepting Tunnel relay connections.");

            Requires.Argument(
                !string.IsNullOrEmpty(endpoint?.ClientRelayUri),
                nameof(tunnel),
                $"The tunnel client relay endpoint URI is missing.");

            var random = new Random();
            var clientRelayUri = endpoint.ClientRelayUri;

            // The access token might be null if connecting to a tunnel that allows anonymous access.
            string? accessToken = null!;
            tunnel.AccessTokens?.TryGetValue(TunnelAccessScopes.Connect, out accessToken);

            Trace.TraceInformation("Connecting to client tunnel relay {0}", clientRelayUri);
            try
            {
                var stream = await StreamFactory.CreateRelayStreamAsync(
                    new Uri(clientRelayUri, UriKind.Absolute),
                    accessToken,
                    WebSocketSubProtocol,
                    cancellation);
                try
                {
                    await StartSshSessionAsync(stream, cancellation);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }
            catch (Exception exception)
            {
                throw new TunnelConnectionException("Failed to connect to tunnel relay.", exception);
            }
        }
    }
}
