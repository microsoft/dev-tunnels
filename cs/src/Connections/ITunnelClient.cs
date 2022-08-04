// <copyright file="ITunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh.Tcp.Events;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Interface for a client capable of making a connection to a tunnel and
    /// forwarding ports over the tunnel.
    /// </summary>
    public interface ITunnelClient : IAsyncDisposable
    {
        /// <summary>
        /// Gets the list of connection modes that this client supports.
        /// </summary>
        IReadOnlyCollection<TunnelConnectionMode> ConnectionModes { get; }

        /// <summary>
        /// Gets list of ports forwarded to client, this collection
        /// contains events to notify when ports are forwarded
        /// </summary>
        ForwardedPortsCollection? ForwardedPorts { get; }

        /// <summary>
        ///  A value indicating whether local connections for forwarded ports are accepted.
        /// </summary>
        bool AcceptLocalConnectionsForForwardedPorts { get; set; }

        /// <summary>
        /// Gets the connection status.
        /// </summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>
        /// Gets the exception that caused disconnection.
        /// Null if not yet connected or disconnection was caused by disposing of this object.
        /// </summary>
        Exception? DisconnectException { get; }

        /// <summary>
        /// Connects to a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel to connect to.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// Once connected, tunnel ports are forwarded by the host.
        /// The client either needs to be logged in as the owner identity, or have
        /// an access token with "connect" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not found.</exception>
        /// <exception cref="UnauthorizedAccessException">The client does not have
        /// access to connect to the tunnel.</exception>
        /// <exception cref="TunnelConnectionException">The client failed to connect to the
        /// tunnel, or connected but encountered a protocol errror.</exception>
        Task ConnectAsync(Tunnel tunnel, CancellationToken cancellation)
            => ConnectAsync(tunnel, hostId: null, cancellation);

        /// <summary>
        /// Connects to a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel to connect to.</param>
        /// <param name="hostId">ID of the tunnel host to connect to, if there are multiple
        /// hosts accepting connections on the tunnel, or null to connect to a single host
        /// (most common).</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// Once connected, tunnel ports are forwarded by the host.
        /// The client either needs to be logged in as the owner identity, or have
        /// an access token with "connect" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not found.</exception>
        /// <exception cref="UnauthorizedAccessException">The client does not have
        /// access to connect to the tunnel.</exception>
        /// <exception cref="TunnelConnectionException">The client failed to connect to the
        /// tunnel, or connected but encountered a protocol errror.</exception>
        Task ConnectAsync(Tunnel tunnel, string? hostId, CancellationToken cancellation);

        /// <summary>
        /// Waits for the specified port to be forwarded by the remote host.
        /// </summary>
        /// <param name="forwardedPort">Remote port to wait for.</param>
        /// <param name="cancellation">Cancellation token for the request</param>
        /// <exception cref="InvalidOperationException">Throws if called before the client has connected.</exception>
        Task WaitForForwardedPortAsync(int forwardedPort, CancellationToken cancellation);

        /// <summary>
        /// Opens a stream connected to a remote port for clients which cannot or do not want to forward local TCP ports.
        /// Returns null if the session gets closed, or the port is no longer forwarded by the host.
        /// </summary>
        /// <remarks>
        /// Set <see cref="AcceptLocalConnectionsForForwardedPorts"/> to <c>false</c> before calling <see cref="ConnectAsync(Tunnel, CancellationToken)"/> to ensure
        /// that forwarded tunnel ports won't get local TCP listeners.
        /// </remarks>
        /// <param name="forwardedPort">Remote port to connect to.</param>
        /// <param name="cancellation">Cancellation token for the request.</param>
        /// <returns>A <see cref="Task{Stream}"/> representing the result of the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">If the tunnel is not yet connected and hasn't started connecting.</exception>
        Task<Stream?> ConnectToForwardedPortAsync(int forwardedPort, CancellationToken cancellation);

        /// <summary>
        /// Event handler for refreshing the tunnel access token.
        /// The tunnel client will fire this event when it is not able to use the access token it got from the tunnel.
        /// </summary>
        event EventHandler<RefreshingTunnelAccessTokenEventArgs>? RefreshingTunnelAccessToken;

        /// <summary>
        /// Connection status changed event.
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    }
}
