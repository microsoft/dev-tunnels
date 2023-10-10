// <copyright file="RefreshingTunnelEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Event args for tunnel refresh.
/// </summary>
public class RefreshingTunnelEventArgs : EventArgs
{
    /// <summary>
    /// Create a new instance of <see cref="RefreshingTunnelEventArgs"/>.
    /// </summary>
    public RefreshingTunnelEventArgs(
        string tunnelAccessScope,
        Tunnel? tunnel,
        ITunnelManagementClient? managementClient,
        bool includePorts,
        CancellationToken cancellation)
    {
        TunnelAccessScope = Requires.NotNull(tunnelAccessScope, nameof(tunnelAccessScope));
        Tunnel = tunnel;
        ManagementClient = managementClient;
        IncludePorts = includePorts;
        Cancellation = cancellation;
    }

    /// <summary>
    /// Get tunnel access scope for this tunnel client or host.
    /// </summary>
    public string TunnelAccessScope { get; }

    /// <summary>
    /// Get the tunnel being refreshed.
    /// </summary>
    public Tunnel? Tunnel { get; }

    /// <summary>
    /// Management client used for connections.
    /// </summary>
    public ITunnelManagementClient? ManagementClient { get; }

    /// <summary>
    /// Get a value indicating whether ports need to be included into the refreshed tunnel.
    /// </summary>
    public bool IncludePorts { get; }

    /// <summary>
    /// Cancellation token that event handler may observe when it asynchronously fetches the tunnel.
    /// </summary>
    public CancellationToken Cancellation { get; }

    /// <summary>
    /// Tunnel refresh task the event handler may set to asynchnronously fetch the tunnel.
    /// The result of the task may be a new tunnel or null if it couldn't get the tunnel or the tunnel doesn't exist.
    /// </summary>
    public Task<Tunnel?>? TunnelRefreshTask { get; set; }
}
