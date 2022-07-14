// <copyright file="ITunnelRelayConnector.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Tunnel connector.
/// </summary>
public interface ITunnelConnector
{
    /// <summary>
    /// Connect or reconnect tunnel SSH session.
    /// </summary>
    Task ConnectSessionAsync(bool isReconnect, CancellationToken cancellation);
}
