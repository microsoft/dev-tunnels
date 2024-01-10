// <copyright file="MultiModeTunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh.Tcp.Events;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel client implementation that selects one of multiple available connection modes.
/// </summary>
public class MultiModeTunnelClient : TunnelConnection, ITunnelClient
{
    /// <summary>
    /// Creates a new instance of the <see cref="MultiModeTunnelClient" /> class
    /// that can select from among multiple single-mode clients.
    /// </summary>
    public MultiModeTunnelClient(IEnumerable<ITunnelClient> clients, ITunnelManagementClient managementClient, TraceSource trace) : base(managementClient, trace)
    {
        Clients = new List<ITunnelClient>(Requires.NotNull(clients, nameof(clients)));
        Requires.Argument(
            Clients.Count() > 0,
            nameof(clients),
            "At least one tunnel client is required.");

        // TODO: Subscribe to clients RefreshingTunnelAccessToken event and call TunnelBase.RefreshTunnelAccessTokenAsync() to get the tunnel access token.
    }

    /// <summary>
    /// Gets the list of clients that may be used to connect to the tunnel.
    /// </summary>
    public IEnumerable<ITunnelClient> Clients { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<TunnelConnectionMode> ConnectionModes
        => Clients.SelectMany((c) => c.ConnectionModes).Distinct().ToArray();

#pragma warning disable CS0067 // Not used
    /// <inheritdoc />
    public event EventHandler<PortForwardingEventArgs>? PortForwarding;
#pragma warning restore CS0067


    /// <inheritdoc />
    public ForwardedPortsCollection? ForwardedPorts => throw new NotImplementedException();

#pragma warning disable CS0067 // Not used
    /// <inheritdoc />
    public event EventHandler<ForwardedPortConnectingEventArgs>? ForwardedPortConnecting;
#pragma warning restore CS0067

    /// <inheritdoc />
    public bool AcceptLocalConnectionsForForwardedPorts
    {
        get => Clients.Any(c => c.AcceptLocalConnectionsForForwardedPorts);
        set
        {
            foreach (var client in Clients)
            {
                client.AcceptLocalConnectionsForForwardedPorts = value;
            }
        }
    }

    /// <inheritdoc />
    public IPAddress LocalForwardingHostAddress
    {
        get => Clients.FirstOrDefault()?.LocalForwardingHostAddress ?? IPAddress.Loopback;
        set
        {
            foreach (var client in Clients)
            {
                client.LocalForwardingHostAddress = value;
            }
        }
    }

    /// <inheritdoc />
    protected override string TunnelAccessScope => TunnelAccessScopes.Connect;

    /// <inheritdoc />
    public override async Task ConnectAsync(
        Tunnel tunnel,
        TunnelConnectionOptions? options,
        CancellationToken cancellation = default)
    {
        Requires.NotNull(tunnel, nameof(tunnel));

        // TODO: Filter tunnel endpoints by host ID, if specified.
        // TODO: Match tunnel endpoints to client connection modes.

        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task WaitForForwardedPortAsync(int forwardedPort, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override async Task DisposeConnectionAsync()
    {
        await base.DisposeConnectionAsync();

        var disposeTasks = new List<Task>();

        foreach (var client in Clients)
        {
            disposeTasks.Add(client.DisposeAsync().AsTask());
        }

        await Task.WhenAll(disposeTasks);
    }

    /// <inheritdoc />
    public Task<Stream?> ConnectToForwardedPortAsync(int forwardedPort, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RefreshPortsAsync(CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }
}
