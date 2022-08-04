// <copyright file="RefreshingTunnelAccessTokenEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Event args for tunnel access token refresh event.
/// </summary>
public class RefreshingTunnelAccessTokenEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="RefreshingTunnelAccessTokenEventArgs"/> class.
    /// </summary>
    public RefreshingTunnelAccessTokenEventArgs(string tunnelAccessScope, CancellationToken cancellation)
    {
        TunnelAccessScope = Requires.NotNull(tunnelAccessScope, nameof(tunnelAccessScope));
        Cancellation = cancellation;
    }

    /// <summary>
    /// Tunnel access scope to get the token for.
    /// </summary>
    public string TunnelAccessScope { get; }

    /// <summary>
    /// Cancellation token that event handler may observe when it asynchronously fetches the tunnel access token.
    /// </summary>
    public CancellationToken Cancellation { get; }

    /// <summary>
    /// Token task the event handler may set to asynchnronously fetch the token.
    /// The result of the task may be a new tunnel access token or null if it couldn't get the token.
    /// </summary>
    public Task<string?>? TunnelAccessTokenTask { get; set; }
}
