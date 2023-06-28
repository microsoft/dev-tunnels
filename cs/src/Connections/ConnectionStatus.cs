// <copyright file="ConnectionStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel client or host connection status.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// The connection has not started yet. This is the initial status.
    /// </summary>
    None,

    /// <summary>
    /// Connecting (if changed from None) or reconnecting (if changed from Connected) to the service.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connecting and refreshing the tunnel access token to connect with.
    /// </summary>
    RefreshingTunnelAccessToken,

    /// <summary>
    /// Connected to the service.
    /// </summary>
    Connected,

    /// <summary>
    /// Disconnected from the service and could not reconnect either due to disposal, service down, tunnel deleted, or token expiration. This is the final status.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Refreshing tunnel host public key.
    /// </summary>
    RefreshingTunnelHostPublicKey,
}
