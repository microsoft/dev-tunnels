// <copyright file="TunnelRelayStreamFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Default factory for creating streams to a tunnel relay.
    /// </summary>
    public class TunnelRelayStreamFactory : ITunnelRelayStreamFactory
    {
        /// <inheritdoc/>
        public virtual async Task<Stream> CreateRelayStreamAsync(
            Uri relayUri,
            string? accessToken,
            string connectionType,
            CancellationToken cancellation)
        {
            void ConfigureWebSocketOptions(ClientWebSocketOptions options)
            {
                options.AddSubProtocol(connectionType);
                options.UseDefaultCredentials = false;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.SetRequestHeader("Authorization", "tunnel " + accessToken);
                }
            }

            var stream = await WebSocketStream.ConnectToWebSocketAsync(
                relayUri, ConfigureWebSocketOptions, cancellation);
            return stream;
        }
    }
}
