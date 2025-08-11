// <copyright file="TunnelRelayTunnelClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel client implementation that connects via a tunnel relay.
/// </summary>
public class TunnelRelayTunnelClient : TunnelClient
{
    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 client protocol.
    /// </summary>
    public override string WebSocketSubProtocol => ClientWebSocketSubProtocol;

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 client protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public override string WebSocketSubProtocolV2 => ClientWebSocketSubProtocolV2;

    /// <summary>
    /// Creates a new instance of a client that connects to a tunnel via a tunnel relay.
    /// </summary>
    public TunnelRelayTunnelClient(TraceSource trace) : this(managementClient: null, trace)
    {
    }

    /// <summary>
    /// Creates a new instance of a client that connects to a tunnel via a tunnel relay.
    /// </summary>
    public TunnelRelayTunnelClient(ITunnelManagementClient? managementClient, TraceSource trace) : base(managementClient, trace) { }

    /// <inheritdoc />
    public override IReadOnlyCollection<TunnelConnectionMode> ConnectionModes
         => new[] { TunnelConnectionMode.TunnelRelay };

    /// <summary>
    /// Tunnel has been assigned to or changed.
    /// Update tunnel access token, relay URI, and host public key from the tunnel.
    /// </summary>
    protected override void OnTunnelChanged()
    {
        base.OnTunnelChanged();
        if (Tunnel?.Endpoints?.Length > 0)
        {
            var endpointGroups = Tunnel.Endpoints.GroupBy((ep) => ep.HostId).ToArray();
            IGrouping<string?, TunnelEndpoint> endpoints;
            if (HostId != null)
            {
                endpoints = endpointGroups.SingleOrDefault((g) => g.Key == HostId) ??
                    throw new InvalidOperationException(
                        "The specified host is not currently accepting connections to the tunnel.");
            }
            else if (endpointGroups.Length > 1)
            {
                throw new InvalidOperationException(
                    "There are multiple hosts for the tunnel. Specify a host ID to connect to.");
            }
            else
            {
                endpoints = endpointGroups.Single();
            }

            var endpoint = endpoints
                .OfType<TunnelRelayTunnelEndpoint>()
                .SingleOrDefault() ??
                throw new InvalidOperationException(
                    "The host is not currently accepting Tunnel relay connections.");

            Requires.Argument(
                !string.IsNullOrEmpty(endpoint?.ClientRelayUri),
                nameof(Tunnel),
                $"The tunnel client relay endpoint URI is missing.");

            RelayUri = new Uri(endpoint.ClientRelayUri, UriKind.Absolute);
            this.HostPublicKeys = endpoint.HostPublicKeys;
        }
        else
        {
            RelayUri = null;
            this.HostPublicKeys = null;
        }
    }

    /// <inheritdoc />
    protected override Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation)
    {
        Requires.NotNull(RelayUri!, nameof(this.RelayUri));
        ITunnelConnector result = new RelayTunnelConnector(this);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Connect to the clientRelayUri using accessToken.
    /// </summary>
    protected async Task ConnectAsync(
        string clientRelayUri,
        string? accessToken,
        string[]? hostPublicKeys,
        TunnelConnectionOptions options,
        CancellationToken cancellation)
    {
        Requires.NotNull(clientRelayUri, nameof(clientRelayUri));
        RelayUri = new Uri(clientRelayUri, UriKind.Absolute);

        this.accessToken = accessToken;
        this.HostPublicKeys = hostPublicKeys;
        this.connector = new RelayTunnelConnector(this);

        try
        {
            await this.connector.ConnectSessionAsync(
                options,
                isReconnect: false,
                cancellation);
        }
        catch (Exception ex)
        {
            if (Tunnel != null)
            {
                var connectFailedEvent = new TunnelEvent($"{ConnectionRole}_connect_failed");
                connectFailedEvent.Severity = TunnelEvent.Error;
                connectFailedEvent.Details = ex.ToString();
                ManagementClient?.ReportEvent(Tunnel, connectFailedEvent);
            }
            throw;
        }
    }

    /// <summary>
    /// Configures tunnel SSH session with the given stream.
    /// </summary>
    protected override async Task ConfigureSessionAsync(
        Stream stream,
        bool isReconnect,
        TunnelConnectionOptions? options,
        CancellationToken cancellation)
    {
        var session = SshSession;
        if (isReconnect && session != null && !session.IsClosed)
        {
            await session.ReconnectAsync(stream, cancellation);
        }
        else
        {
            await StartSshSessionAsync(stream, options, cancellation);
        }
    }
}
