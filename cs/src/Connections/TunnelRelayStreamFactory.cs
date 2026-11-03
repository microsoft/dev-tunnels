// <copyright file="TunnelRelayStreamFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Connections
{
    /// <summary>
    /// Default factory for creating streams to a tunnel relay.
    /// </summary>
    public class TunnelRelayStreamFactory : ITunnelRelayStreamFactory
    {
        private readonly string? hostId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelRelayStreamFactory"/> class.
        /// </summary>
        public TunnelRelayStreamFactory()
        {
        }

        internal TunnelRelayStreamFactory(string hostId)
        {
            this.hostId = hostId;
        }

        /// <inheritdoc/>
        public virtual async Task<(Stream Stream, string SubProtocol)> CreateRelayStreamAsync(
            Uri relayUri,
            string? accessToken,
            string[] subprotocols,
            TraceSource trace,
            CancellationToken cancellation)
        {
            var isHostConnection =
                Array.IndexOf(subprotocols, TunnelRelayConnection.HostWebSocketSubProtocol) >= 0 ||
                Array.IndexOf(subprotocols, TunnelRelayConnection.HostWebSocketSubProtocolV2) >= 0;

            void ConfigureWebSocketOptions(ClientWebSocketOptions options)
            {
                foreach (var subprotocol in subprotocols)
                {
                    options.AddSubProtocol(subprotocol);
                }
                options.UseDefaultCredentials = false;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.SetRequestHeader("Authorization", "tunnel " + accessToken);
                }

                if (isHostConnection && !string.IsNullOrEmpty(hostId))
                {
                    options.SetRequestHeader(
                        TunnelRelayConnection.HostIdHeaderName, hostId);
                }
            }

            var stream = await WebSocketStream.ConnectToWebSocketAsync(
                relayUri, ConfigureWebSocketOptions, trace, cancellation);
            return (stream, stream.SubProtocol);
        }
    }
}
