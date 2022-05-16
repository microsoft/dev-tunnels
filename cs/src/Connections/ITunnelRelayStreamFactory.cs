// <copyright file="ITunnelRelayStreamFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Interface for a factory capable of creating streams to a tunnel relay.
    /// </summary>
    /// <remarks>
    /// Normally the default <see cref="TunnelRelayStreamFactory" /> can be used. However a
    /// different factory class may be used to customize the connection (or mock the connection
    /// for testing).
    /// </remarks>
    /// <seealso cref="TunnelRelayTunnelHost.StreamFactory" />
    /// <seealso cref="TunnelRelayTunnelClient.StreamFactory" />
    public interface ITunnelRelayStreamFactory
    {
        /// <summary>
        /// Creates a stream connected to a tunnel relay URI.
        /// </summary>
        /// <param name="relayUri">URI of the tunnel relay to connect to.</param>
        /// <param name="accessToken">Tunnel host access token, or null if anonymous.</param>
        /// <param name="connectionType">Type of stream connection to create, aka
        /// websocket sub-protocol.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Stream connected to the relay.</returns>
        Task<Stream> CreateRelayStreamAsync(
            Uri relayUri,
            string? accessToken,
            string connectionType,
            CancellationToken cancellation);
    }
}
