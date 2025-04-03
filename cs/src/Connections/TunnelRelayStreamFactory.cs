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
        /// <inheritdoc/>
        public virtual async Task<(Stream Stream, string SubProtocol)> CreateRelayStreamAsync(
            Uri relayUri,
            string? accessToken,
            string[] subprotocols,
            TraceSource trace,
            CancellationToken cancellation)
        {
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
            }

            var stream = await WebSocketStream.ConnectToWebSocketAsync(
                relayUri, ConfigureWebSocketOptions, trace, cancellation);
            return (stream, stream.SubProtocol);
        }
    }
}
