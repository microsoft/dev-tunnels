// <copyright file="ITunnelHost.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Interface for a host capable of sharing local ports via a tunnel and accepting
    /// tunneled connections to those ports.
    /// </summary>
    public interface ITunnelHost : IAsyncDisposable
    {
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
        /// Connects to a tunnel as a host and starts accepting incoming connections
        /// to local ports as defined on the tunnel.
        /// </summary>
        /// <param name="tunnel">Information about the tunnel to connect to.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// The host either needs to be logged in as the owner identity, or have
        /// an access token with "host" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not found.</exception>
        /// <exception cref="UnauthorizedAccessException">The host does not have
        /// access to host the tunnel.</exception>
        /// <exception cref="TunnelConnectionException">The host failed to connect to the
        /// tunnel, or connected but encountered a protocol errror.</exception>
        Task StartAsync(Tunnel tunnel, CancellationToken cancellation);

        /// <summary>
        /// Adds and starts forwarding a port to a tunnel that is currently being hosted through StartAsync
        /// </summary>
        /// <param name="portToAdd">Port to add to hosted tunnel.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// The host either needs to be logged in as the owner identity, or have
        /// an access token with "host" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not running or port was already added.</exception>
        /// <returns>Added port object as returned by the service</returns>
        Task<TunnelPort> AddPortAsync(
            TunnelPort portToAdd,
            CancellationToken cancellation);

        /// <summary>
        /// Removes a port to a tunnel that is currently being hosted through StartAsync
        /// </summary>
        /// <param name="portNumberToRemove">Port number to be removed from hosted tunnel.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// The host either needs to be logged in as the owner identity, or have
        /// an access token with "host" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not running or did not have ports.</exception>
        /// <returns>Boolean of if port was deleted</returns>
        Task<bool> RemovePortAsync(
            ushort portNumberToRemove,
            CancellationToken cancellation);

        /// <summary>
        /// Updates a port in a tunnel that is currently being hosted through StartAsync
        /// </summary>
        /// <param name="updatedPort">Port to update in hosted tunnel.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <remarks>
        /// The host either needs to be logged in as the owner identity, or have
        /// an access token with "host" scope for the tunnel.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The tunnel was not running.</exception>
        /// <returns>Updated port object, refreshed from the service</returns>
        Task<TunnelPort> UpdatePortAsync(
            TunnelPort updatedPort,
            CancellationToken cancellation);

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
