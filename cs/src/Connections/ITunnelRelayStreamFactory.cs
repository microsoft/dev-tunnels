// <copyright file="ITunnelRelayStreamFactory.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Connections
{
    /// <summary>
    /// Interface for a factory capable of creating streams to a tunnel relay.
    /// </summary>
    /// <remarks>
    /// Normally the default <see cref="TunnelRelayStreamFactory" /> can be used. However a
    /// different factory class may be used to customize the connection (or mock the connection
    /// for testing).
    /// </remarks>
    /// <seealso cref="TunnelRelayConnection.StreamFactory" />
    public interface ITunnelRelayStreamFactory
    {
        /// <summary>
        /// Creates a stream connected to a tunnel relay URI.
        /// </summary>
        /// <param name="relayUri">URI of the tunnel relay to connect to.</param>
        /// <param name="accessToken">Tunnel host access token, or null if anonymous.</param>
        /// <param name="subprotocols">One or more websocket subprotocols (relay connection
        /// protocols).</param>
        /// <param name="trace">Trace source for logging.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Stream connected to the relay, along with the actual subprotocol that was
        /// selected by the server.</returns>
        Task<(Stream Stream, string SubProtocol)> CreateRelayStreamAsync(
            Uri relayUri,
            string? accessToken,
            string[] subprotocols,
            TraceSource trace,
            CancellationToken cancellation);
    }
}
