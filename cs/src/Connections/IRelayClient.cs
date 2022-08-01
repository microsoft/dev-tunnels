// <copyright file="IRelayClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh;

namespace Microsoft.VsSaaS.TunnelService;

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
    /// Create stream to the tunnel.
    /// </summary>
    Task<Stream> CreateSessionStreamAsync(CancellationToken cancellation);

    /// <summary>
    /// Configures tunnel SSH session with the given stream.
    /// </summary>
    Task ConfigureSessionAsync(Stream stream, bool isReconnect, CancellationToken cancellation);

    /// <summary>
    /// Closes tunnel SSH session due to an error or exception.
    /// </summary>
    Task CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception);

    /// <summary>
    /// Refresh tunnel access token. This may be useful when the Relay service responds with 401 Unauthorized.
    /// </summary>
    Task<bool> RefreshTunnelAccessTokenAsync(CancellationToken cancellation);
}
