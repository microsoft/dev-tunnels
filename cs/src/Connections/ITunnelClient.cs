// <copyright file="ITunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh.Tcp.Events;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

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
    /// Event raised when a port is about to be forwarded to the client.
    /// </summary>
    /// <remarks>
    /// The application may cancel this event to prevent specific port(s) from being
    /// forwarded to the client. Cancelling prevents the tunnel client from listening on a
    /// local socket for the port, AND prevents use of <see cref="ConnectToForwardedPortAsync"/>
    /// to open a direct stream connection to the port.
    /// 
    /// This event is still fired when <see cref="AcceptLocalConnectionsForForwardedPorts" />
    /// is false.
    /// </remarks>
    event EventHandler<PortForwardingEventArgs>? PortForwarding;

    /// <summary>
    /// Gets list of ports forwarded to client, this collection
    /// contains events to notify when ports are forwarded
    /// </summary>
    ForwardedPortsCollection? ForwardedPorts { get; }

    /// <summary>
    /// An event which fires when a connection is made to the forwarded port.
    /// </summary>
    event EventHandler<ForwardedPortConnectingEventArgs>? ForwardedPortConnecting;

    /// <summary>
    /// Gets or sets a value indicating whether local connections for forwarded ports are
    /// accepted.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// </remarks>
    bool AcceptLocalConnectionsForForwardedPorts { get; set; }

    /// <summary>
    /// Gets or sets the local network interface address that the tunnel client listens on when
    /// accepting connections for forwarded ports.
    /// </summary>
    /// <remarks>
    /// The default value is the loopback address (127.0.0.1). Applications may set this to the
    /// address indicating any interface (0.0.0.0) or to the address of a specific interface.
    /// The tunnel client supports both IPv4 and IPv6 when listening on either loopback or
    /// any interface.
    /// </remarks>
    IPAddress LocalForwardingHostAddress { get; set; }

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
    /// tunnel, or connected but encountered a protocol error.</exception>
    Task ConnectAsync(Tunnel tunnel, CancellationToken cancellation = default)
        => ConnectAsync(tunnel, options: null, cancellation);

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
    /// tunnel, or connected but encountered a protocol error.</exception>
    [Obsolete("Use ConnectAsync(Tunnel, TunnelConnectionOptions, CancellationToken) instead.")]
    Task ConnectAsync(Tunnel tunnel, string? hostId, CancellationToken cancellation)
        => ConnectAsync(
            tunnel,
            new TunnelConnectionOptions { HostId = hostId }, cancellation);

    /// <summary>
    /// Connects to a tunnel.
    /// </summary>
    /// <param name="tunnel">Tunnel to connect to.</param>
    /// <param name="options">Options for the connection.</param>
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
    /// tunnel, or connected but encountered a protocol error.</exception>
    Task ConnectAsync(
        Tunnel tunnel,
        TunnelConnectionOptions? options,
        CancellationToken cancellation = default);

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
    /// Sends a request to the host to refresh ports that were updated using the management API,
    /// and waits for the refresh to complete.
    /// </summary>
    /// <param name="cancellation">Cancellation token.</param>
    /// <remarks>
    /// After calling <see cref="ITunnelManagementClient.CreateTunnelPortAsync"/> or
    /// <see cref="ITunnelManagementClient.DeleteTunnelPortAsync"/>, call this method to have a
    /// connected client notify the host to update its cached list of ports. Any added or
    /// removed ports will then propagate back to the set of ports forwarded by the current
    /// client. After the returned task has completed, any newly added ports are usable from
    /// the current client.
    /// </remarks>
    Task RefreshPortsAsync(CancellationToken cancellation);

    /// <summary>
    /// Event handler for refreshing the tunnel access token.
    /// The tunnel client will fire this event when it is not able to use the access token it got from the tunnel.
    /// </summary>
    event EventHandler<RefreshingTunnelAccessTokenEventArgs>? RefreshingTunnelAccessToken;

    /// <summary>
    /// Connection status changed event.
    /// </summary>
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Event raised when a keep-alive message response is not received.
    /// </summary>
    /// <remarks>
    /// The event args provide the count of keep-alive messages that did not get a response within the
    /// configured <see cref="TunnelConnectionOptions.KeepAliveIntervalInSeconds"/>. This callback is only invoked
    /// if the keep-alive interval is greater than 0.
    /// </remarks>
    public event EventHandler<SshKeepAliveEventArgs>? KeepAliveFailed;

    /// <summary>
    /// Event raised when a keep-alive message response is received.
    /// </summary>
    /// <remarks>
    /// The event args provide the count of keep-alive messages that got a response within the
    /// configured <see cref="TunnelConnectionOptions.KeepAliveIntervalInSeconds"/>. This callback is only invoked
    /// if the keep-alive interval is greater than 0.
    /// </remarks>
    public event EventHandler<SshKeepAliveEventArgs>? KeepAliveSucceeded;
}

