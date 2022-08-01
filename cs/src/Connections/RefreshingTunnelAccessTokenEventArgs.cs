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
    public RefreshingTunnelAccessTokenEventArgs(string tunnelAccessScope)
    {
        TunnelAccessScope = Requires.NotNull(tunnelAccessScope, nameof(tunnelAccessScope));
    }

    /// <summary>
    /// Tunnel access scope to get the token for.
    /// </summary>
    public string TunnelAccessScope { get; }

    /// <summary>
    /// Optional token provider function the event handler may set to asynchnronously fetch the token.
    /// The arguments are: tunnel access scope and cancellation token.
    /// The result is the task that returns the new tunnel access token or null if it couldn't fetch the token.
    /// </summary>
    public Func<string, CancellationToken, Task<string?>>? TunnelAccessTokenProvider { get; set; }

    /// <summary>
    /// Tunnel access token that the event handler may set synchronously.
    /// </summary>
    public string? TunnelAccessToken { get; set; }
}
