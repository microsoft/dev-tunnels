// <copyright file="IRelayClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Relay service client.
/// </summary>
internal interface IRelayClient
{
    /// <summary>
    /// Get tunnel access scope for this tunnel client or host.
    /// </summary>
    string TunnelAccessScope { get; }

    /// <summary>
    /// Gets the trace source.
    /// </summary>
    TraceSource Trace { get; }

    /// <summary>
    /// Get the tunnel connection role, either "client", or "host".
    /// </summary>
    string ConnectionRole { get; }

    /// <summary>
    /// Get relay client dispose token.
    /// </summary>
    CancellationToken DisposeToken { get; }

    /// <summary>
    /// Start connecting relay client.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If relay client is disposed.</exception>
    void StartConnecting();

    /// <summary>
    /// Finish connecting relay client.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If relay client is disposed.</exception>
    void FinishConnecting(SshDisconnectReason reason, Exception? disconnectException);

    /// <summary>
    /// Create stream to the tunnel.
    /// If this method succeeds, <see cref="ConfigureSessionAsync(Stream, bool, TunnelConnectionOptions?, CancellationToken)"/> will be called.
    /// If this method fails, depending on <see cref="TunnelConnectionOptions.EnableRetry"/> and failure, tunnel client may try reconnecting.
    /// </summary>
    Task<Stream> CreateSessionStreamAsync(CancellationToken cancellation);

    /// <summary>
    /// Gets the connection protocol (websocket subprotocol) that was negotiated between client and server.
    /// </summary>
    string? ConnectionProtocol { get; }

    /// <summary>
    /// Configures tunnel SSH session with the given stream.
    /// If this method succeeds, the SSH session must be connected and ready.
    /// If this method fails, depending on <see cref="TunnelConnectionOptions.EnableRetry"/> and failure, tunnel client may try reconnecting.
    /// </summary>
    Task ConfigureSessionAsync(Stream stream, bool isReconnect, TunnelConnectionOptions? options, CancellationToken cancellation);

    /// <summary>
    /// Closes tunnel SSH session due to an error or exception happened during connection.
    /// Depending on <see cref="TunnelConnectionOptions.EnableRetry"/> and nature of the error,
    /// the connection may or may not do another attempt.
    /// </summary>
    Task CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception);

    /// <summary>
    /// Refresh tunnel access token. This may be useful when the Relay service responds with 401 Unauthorized.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If the connection is disposed or disconnected.</exception>
    /// <exception cref="ArgumentException">If current status is not <see cref="ConnectionStatus.Connecting"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">If the refreshed token is expired.</exception>
    /// <exception cref="OperationCanceledException">If refresh is cancelled or connection disposed.</exception>
    Task<bool> RefreshTunnelAccessTokenAsync(CancellationToken cancellation);

    /// <summary>
    /// Notifies about a connection retry, giving the relay client a chance to delay or cancel it.
    /// </summary>
    void OnRetrying(RetryingTunnelConnectionEventArgs e);
}
