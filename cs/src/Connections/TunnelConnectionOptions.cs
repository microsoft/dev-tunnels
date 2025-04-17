// <copyright file="TunnelConnectionOptions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Threading;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Options for a tunnel host or client connection.
/// </summary>
/// <seealso cref="ITunnelHost.ConnectAsync(Tunnel, TunnelConnectionOptions?, CancellationToken)"/>
public class TunnelConnectionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the connection will be automatically retried after
    /// a connection failure.
    /// </summary>
    /// <remarks>
    /// The default value is true. When enabled, retries continue until the connection is
    /// successful, the cancellation token is cancelled, or an unrecoverable error is encountered.
    ///
    /// Recoverable errors include network connectivity issues, authentication issues (e.g. expired
    /// access token which may be refreshed before retrying), and service temporarily unavailable
    /// (HTTP 503). For rate-limiting errors (HTTP 429) only a limited number of retries are
    /// attempted before giving up.
    ///
    /// Retries are performed with exponential backoff, starting with a 100ms delay and doubling
    /// up to a maximum 12s delay, with further retries using the same max delay.
    ///
    /// Note after the initial connection succeeds, the host or client may still become disconnected
    /// at any time after that due to a network disruption or a relay service upgrade. When that
    /// happens, the <see cref="EnableReconnect" /> option controls whether an automatic reconnect
    /// will be attempted. Reconnection has the same retry behavior.
    ///
    /// Listen to the <see cref="TunnelConnection.RetryingTunnelConnection" /> event to be notified
    /// when the connection is retrying.
    /// </remarks>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the connection will attempt to automatically
    /// reconnect (with no data loss) after a disconnection. 
    /// </summary>
    /// <remarks>
    /// The default value is true.
    ///
    /// If reconnection fails, or is not enabled, the application may still attempt to connect
    /// the client again, however in that case no state is preserved.
    ///
    /// Listen to the <see cref="TunnelConnection.ConnectionStatusChanged" /> event to be notified
    /// when reconnection or disconnection occurs.
    /// </remarks>
    public bool EnableReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the ID of the tunnel host to connect to, if there are multiple
    /// hosts accepting connections on the tunnel, or null to connect to a single host
    /// (most common). This option applies only to client connections.
    /// </summary>
    public string? HostId { get; set; }

    /// <summary>
    /// Gets or sets the ssh keep-alive interval in seconds. The default value is 0, which means no keep-alive.
    /// When set to a positive value, the client will send keep-alive messages to the server
    /// and calls the <see cref="TunnelConnection.KeepAliveFailed"/> callback with the number of times
    /// the keep-alive is sent without a response.
    /// 
    /// The KeepAliveFailed event is raised at the time of sending the next keep-alive request,
    /// for example if the interval is set to 10 seconds the first request is sent after 10 seconds of inactivity
    /// and waits for 10 more seconds to call the KeepAliveFailed callback before sending another
    /// keep-alive request.
    /// </summary>
    public int KeepAliveIntervalInSeconds { get; set; } = 0;
}
