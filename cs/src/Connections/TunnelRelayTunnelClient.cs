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
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel client implementation that connects via a tunnel relay.
/// </summary>
public class TunnelRelayTunnelClient : TunnelClient, IRelayClient
{
    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 client protocol.
    /// </summary>
    public const string WebSocketSubProtocol = "tunnel-relay-client";

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 client protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public const string WebSocketSubProtocolV2 = "tunnel-relay-client-v2-dev";

    private Uri? relayUri;

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

    /// <summary>
    /// Gets or sets a factory for creating relay streams.
    /// </summary>
    /// <remarks>
    /// Normally the default <see cref="TunnelRelayStreamFactory" /> can be used. However a
    /// different factory class may be used to customize the connection (or mock the connection
    /// for testing).
    /// </remarks>
    public ITunnelRelayStreamFactory StreamFactory { get; set; } = new TunnelRelayStreamFactory();

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

            this.relayUri = new Uri(endpoint.ClientRelayUri, UriKind.Absolute);
            this.HostPublicKeys = endpoint.HostPublicKeys;
        }
        else
        {
            this.relayUri = null;
            this.HostPublicKeys = null;
        }
    }

    /// <inheritdoc />
    protected override Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation)
    {
        Requires.NotNull(this.relayUri!, nameof(this.relayUri));
        ITunnelConnector result = new RelayTunnelConnector(this);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Connect to the clientRelayUri using accessToken.
    /// </summary>
    protected Task ConnectAsync(
        string clientRelayUri,
        string? accessToken,
        string[]? hostPublicKeys,
        CancellationToken cancellation)
    {
        Requires.NotNull(clientRelayUri, nameof(clientRelayUri));
        return ConnectTunnelSessionAsync(
            (cancellation) =>
            {
                this.relayUri = new Uri(clientRelayUri, UriKind.Absolute);
                this.accessToken = accessToken;
                this.HostPublicKeys = hostPublicKeys;
                this.connector = new RelayTunnelConnector(this);
                return this.connector.ConnectSessionAsync(isReconnect: false, cancellation);
            },
            cancellation);
    }

    /// <summary>
    /// Create stream to the tunnel.
    /// </summary>
    protected virtual async Task<Stream> CreateSessionStreamAsync(CancellationToken cancellation)
    {
        var protocols = Environment.GetEnvironmentVariable("DEVTUNNELS_PROTOCOL_VERSION") switch
        {
            "1" => new[] { WebSocketSubProtocol },
            "2" => new[] { WebSocketSubProtocolV2 },

            // By default, prefer V2 and fall back to V1.
            _ => new[] { WebSocketSubProtocolV2, WebSocketSubProtocol },
        };

        ValidateAccessToken();
        Trace.TraceInformation("Connecting to client tunnel relay {0}", this.relayUri!.AbsoluteUri);
        var (stream, subprotocol) = await this.StreamFactory.CreateRelayStreamAsync(
            this.relayUri!,
            this.accessToken,
            protocols,
            cancellation);
        Trace.TraceEvent(TraceEventType.Verbose, 0, "Connected with subprotocol '{0}'", subprotocol);
        ConnectionProtocol = subprotocol;
        return stream;
    }

    /// <summary>
    /// Configures tunnel SSH session with the given stream.
    /// </summary>
    protected virtual async Task ConfigureSessionAsync(Stream stream, bool isReconnect, CancellationToken cancellation)
    {
        if (isReconnect && SshSession != null && !SshSession.IsClosed)
        {
            await SshSession.ReconnectAsync(stream, cancellation);
        }
        else
        {
            await StartSshSessionAsync(stream, cancellation);
        }
    }

    #region IRelayClient

    /// <inheritdoc />
    string IRelayClient.TunnelAccessScope => TunnelAccessScope;

    /// <inheritdoc />
    TraceSource IRelayClient.Trace => Trace;

    /// <inheritdoc />
    Task<Stream> IRelayClient.CreateSessionStreamAsync(CancellationToken cancellation) => 
        CreateSessionStreamAsync(cancellation);

    /// <inheritdoc />
    Task IRelayClient.CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception) =>
        CloseSessionAsync(disconnectReason, exception);

    /// <inheritdoc />
    Task IRelayClient.ConfigureSessionAsync(Stream stream, bool isReconnect, CancellationToken cancellation) =>
        ConfigureSessionAsync(stream, isReconnect, cancellation);

    /// <inheritdoc />
    Task<bool> IRelayClient.RefreshTunnelAccessTokenAsync(CancellationToken cancellation) =>
        RefreshTunnelAccessTokenAsync(cancellation);

    /// <inheritdoc />
    void IRelayClient.OnRetrying(RetryingTunnelConnectionEventArgs e) => OnRetrying(e);

    #endregion IRelayClient
}
