// <copyright file="TunnelRelayConnection.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Connections.Messages;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel connection that connects to Relay service
/// </summary>
public abstract class TunnelRelayConnection : TunnelConnection, IRelayClient, IPortForwardMessageFactory
{
    #region Web Sockect Sub Protocols

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 client protocol.
    /// </summary>
    public const string ClientWebSocketSubProtocol = "tunnel-relay-client";

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 client protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public const string ClientWebSocketSubProtocolV2 = "tunnel-relay-client-v2-dev";

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 host protocol.
    /// </summary>
    public const string HostWebSocketSubProtocol = "tunnel-relay-host";

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 host protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public const string HostWebSocketSubProtocolV2 = "tunnel-relay-host-v2-dev";

    #endregion

    /// <summary>
    /// Maximum retry delay, ms.
    /// After the 6th attempt the delay will reach 2^7 * 100ms = 12.8s and stop doubling
    /// </summary>
    public const int RetryMaxDelayMs = 12_800;

    private string? websocketRequestId = null;
    private TunnelConnectionOptions? connectionOptions;
    private Task? reconnectTask;
    private Stopwatch connectionTimer = new();

    /// <summary>
    /// Create a new instance of <see cref="TunnelRelayConnection"/> class.
    /// </summary>
    protected TunnelRelayConnection(ITunnelManagementClient? managementClient, TraceSource trace)
        : base(managementClient, trace)
    {
    }

    /// <summary>
    /// Gets an ID that is unique to this instance of <see cref="TunnelRelayConnection"/>,
    /// useful for correlating connection events over time.
    /// </summary>
    protected virtual string ConnectionId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Connection protocol used to connect to Relay.
    /// </summary>
    public string? ConnectionProtocol
    {
        get;
        protected set;
    }

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 host protocol.
    /// </summary>
    public abstract string WebSocketSubProtocol { get; }

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 host protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public abstract string WebSocketSubProtocolV2 { get; }

    /// <summary>
    /// Get or set the relay endpoint URI.
    /// </summary>
    protected Uri? RelayUri { get; set; }

    /// <summary>
    /// Session used to connect to tunnel.
    /// </summary>
    protected SshClientSession? SshSession { get; set; }

    /// <summary>
    /// Gets or sets a factory for creating relay streams.
    /// </summary>
    /// <remarks>
    /// Normally the default <see cref="TunnelRelayStreamFactory" /> can be used. However a
    /// different factory class may be used to customize the connection (or mock the connection
    /// for testing).
    /// </remarks>
    public ITunnelRelayStreamFactory StreamFactory { get; set; } = new TunnelRelayStreamFactory();

    /// <summary>
    /// Get the disconnect reason.
    /// <see cref="SshDisconnectReason.None"/> if not yet disconnected.
    /// <see cref="SshDisconnectReason.ConnectionLost"/> if network connection was lost and reconnects are not enabled or unsuccesfull.
    /// <see cref="SshDisconnectReason.ByApplication"/> if connected relay connection was disposed.
    /// <see cref="SshDisconnectReason.TooManyConnections"/> if host connection was disconnected because another host connected for the same tunnel.
    /// </summary>
    /// <remarks>
    /// If <see cref="TunnelConnection.DisconnectException"/> is <see cref="SshConnectionException"/> disconnect
    /// reason can also be obtained from <see cref="SshConnectionException.DisconnectReason"/>.
    /// </remarks>
    public SshDisconnectReason DisconnectReason
    {
        get;
        private set;
    }

    /// <inheritdoc/>
    protected override async Task DisposeConnectionAsync()
    {
        if (this.reconnectTask is Task reconnectTask)
        {
            // Reconnect task will be canceled by DisposeToken.
            // It doesn't throw.
            await reconnectTask;
        }

        await CloseSessionAsync();
    }

    /// <summary>
    /// Create tunnel connector for <see cref="Tunnel"/>.
    /// </summary>
    protected abstract Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation);

    /// <summary>
    /// Assign the tunnel and connect to it.
    /// </summary>
    protected async Task ConnectTunnelSessionAsync(
        Tunnel tunnel,
        TunnelConnectionOptions? options,
        CancellationToken cancellation)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        this.connectionOptions = options;

        var isReconnect = Tunnel != null;
        Tunnel = tunnel;
        this.connector ??= await CreateTunnelConnectorAsync(cancellation);

        try
        {
            await this.connector.ConnectSessionAsync(options, isReconnect, cancellation);
        }
        catch (Exception ex)
        {
            var connectFailedEvent = new TunnelEvent($"{ConnectionRole}_connect_failed");
            connectFailedEvent.Severity = TunnelEvent.Error;
            connectFailedEvent.Details = ex.ToString();
            ManagementClient?.ReportEvent(tunnel, connectFailedEvent);
            throw;
        }
    }

    /// <summary>
    /// Event fired when the connection status has changed.
    /// </summary>
    protected override void OnConnectionStatusChanged(
        ConnectionStatus previousConnectionStatus,
        ConnectionStatus connectionStatus)
    {
        TimeSpan duration = this.connectionTimer.Elapsed;
        this.connectionTimer.Restart();

        if (Tunnel != null && ManagementClient != null)
        {
            var statusEvent = new TunnelEvent($"{ConnectionRole}_connection_status");
            statusEvent.Properties = new Dictionary<string, string>
            {
                [nameof(ConnectionStatus)] = connectionStatus.ToString(),
                [$"Previous{nameof(ConnectionStatus)}"] = previousConnectionStatus.ToString(),
            };

            if (previousConnectionStatus != ConnectionStatus.None)
            {
                statusEvent.Properties[$"{previousConnectionStatus}Duration"] = duration.ToString();
            }

            if (IsClientConnection)
            {
                // For client sessions, report the SSH session ID property, which is derived from
                // the SSH key-exchange such that both host and client have the same ID.
                statusEvent.Properties["ClientSessionId"] = SshSession?.GetShortSessionId() ?? string.Empty;
            }
            else
            {
                // For host sessions, there is no SSH encryption or key-exchange.
                // Just use a locally-generated GUID that is unique to this session.
                statusEvent.Properties["HostSessionId"] = ConnectionId;
            }

            if (this.websocketRequestId != null)
            {
                statusEvent.Properties["WebsocketRequestId"] = this.websocketRequestId;
            }

            ManagementClient.ReportEvent(Tunnel, statusEvent);
        }

        base.OnConnectionStatusChanged(previousConnectionStatus, connectionStatus);
    }

    /// <summary>
    /// Start reconnecting if connected, not reconnecting already,
    /// and <paramref name="reason"/> is <see cref="SshDisconnectReason.ConnectionLost"/>.
    /// </summary>
    protected void MaybeStartReconnecting(
        SshSession session,
        SshDisconnectReason reason,
        string? message = null,
        Exception? exception = null)
    {
        var traceMessage = $"Connection to {ConnectionRole} tunnel relay closed.{GetDisconnectReason(reason, message, exception)}";
        lock (DisposeLock)
        {
            if (DisposeToken.IsCancellationRequested ||
                ConnectionStatus == ConnectionStatus.Disconnected)
            {
                // Disposed or disconnected already.
                // This reconnection attempt may be caused by closing SSH session on dispose.
                Trace.TraceInformation(traceMessage);
                return;
            }

            if (exception != null)
            {
                DisconnectReason = reason;
                DisconnectException = exception;
            }

            if (ConnectionStatus != ConnectionStatus.Connected ||
                this.reconnectTask != null)
            {
                // Not connected or already connecting.
                Trace.TraceInformation(traceMessage);
                return;
            }

            if (this.connectionOptions?.EnableReconnect != false &&
                this.connector != null && // The connector may be null if the tunnel client/host was created directly from a stream.
                reason == SshDisconnectReason.ConnectionLost) // Only reconnect if it's connection lost.
            {
                if (Tunnel != null)
                {
                    var reconnectEvent = new TunnelEvent($"{ConnectionRole}_reconnect");
                    reconnectEvent.Severity = TunnelEvent.Warning;
                    reconnectEvent.Details = exception?.ToString() ?? traceMessage;
                    reconnectEvent.Properties = new Dictionary<string, string>
                    {
                        ["ClientSessionId"] = session.GetShortSessionId(),
                    };
                    ManagementClient?.ReportEvent(Tunnel, reconnectEvent);
                }

                Trace.TraceInformation($"{traceMessage}. Reconnecting.");
                var task = ReconnectAsync(DisposeToken);
                this.reconnectTask = !task.IsCompleted ? task : null;
            }
            else
            {
                if (Tunnel != null)
                {
                    var disconnectEvent = new TunnelEvent($"{ConnectionRole}_disconnect");
                    disconnectEvent.Severity = TunnelEvent.Warning;
                    disconnectEvent.Details = exception?.ToString() ?? traceMessage;
                    disconnectEvent.Properties = new Dictionary<string, string>
                    {
                        ["ClientSessionId"] = session.GetShortSessionId(),
                    };
                    ManagementClient?.ReportEvent(Tunnel, disconnectEvent);
                }

                Trace.TraceInformation(traceMessage);
                ConnectionStatus = ConnectionStatus.Disconnected;
            }
        }
    }

    /// <summary>
    /// Create stream to the tunnel.
    /// </summary>
    protected virtual async Task<Stream> CreateSessionStreamAsync(CancellationToken cancellation)
    {
        Requires.NotNull(RelayUri!, nameof(RelayUri));

        if (this.IsClientConnection)
        {
            this.OnReportProgress(Progress.OpeningClientConnectionToRelay);
        }
        else
        {
            this.OnReportProgress(Progress.OpeningHostConnectionToRelay);
        }

        var protocols = Environment.GetEnvironmentVariable("DEVTUNNELS_PROTOCOL_VERSION") switch
        {
            "1" => new[] { WebSocketSubProtocol },
            "2" => new[] { WebSocketSubProtocolV2 },

            // By default, prefer V2 and fall back to V1.
            _ => new[] { WebSocketSubProtocolV2, WebSocketSubProtocol },
        };

        Trace.Verbose("Connecting to {0} tunnel relay {1}", ConnectionRole, RelayUri.AbsoluteUri);
        var (stream, subprotocol) = await this.StreamFactory.CreateRelayStreamAsync(
            RelayUri,
            this.accessToken,
            protocols,
            Trace,
            cancellation);
        Trace.TraceEvent(TraceEventType.Verbose, 0, "Connected with subprotocol '{0}'", subprotocol);

        this.websocketRequestId = (stream as WebSocketStream)?.RequestId;

        if (this.IsClientConnection)
        {
            this.OnReportProgress(Progress.OpenedClientConnectionToRelay);
        }
        else
        {
            this.OnReportProgress(Progress.OpenedHostConnectionToRelay);
        }

        ConnectionProtocol = subprotocol;

        return stream;
    }

    /// <summary>
    /// Configures tunnel SSH session with the given stream.
    /// If this method succeeds, the SSH session must be connected and ready.
    /// If this method fails, depending on <see cref="TunnelConnectionOptions.EnableRetry"/> and failure, tunnel client my try reconnecting.
    /// </summary>
    protected abstract Task ConfigureSessionAsync(
        Stream stream,
        bool isReconnect,
        TunnelConnectionOptions? options,
        CancellationToken cancellation);

    /// <summary>
    /// Close tunnel session with the given reason and exception.
    /// </summary>
    /// <remarks>
    /// This is used by <see cref="ITunnelConnector"/> when it couldn't connect the tunnel SSH session.
    /// </remarks>
    protected virtual async Task CloseSessionAsync(
        SshDisconnectReason disconnectReason = SshDisconnectReason.ByApplication,
        Exception? exception = null)
    {
        lock (DisposeLock)
        {
            if (ConnectionStatus != ConnectionStatus.Disconnected)
            {
                if (exception != null)
                {
                    DisconnectReason = disconnectReason;
                    DisconnectException = exception;
                }

                if (DisconnectReason == SshDisconnectReason.None)
                {
                    DisconnectReason = disconnectReason;
                }
            }
        }

        var session = SshSession;
        if (session == null)
        {
            return;
        }

        UnsubscribeSessionEvents(session);
        if (!session.IsClosed && session.IsConnected)
        {
            if (exception != null)
            {
                await session.CloseAsync(disconnectReason, exception);
            }
            else
            {
                await session.CloseAsync(disconnectReason);
            }
        }

        // Set the connection status to disconnected before setting SshSession to null,
        // so the session ID can be reported in the disconnect event.
        ConnectionStatus = ConnectionStatus.Disconnected;
        SshSession = null;

        // Closing the SSH session does nothing if the session is in disconnected state,
        // which may happen for a reconnectable session when the connection drops.
        // Disposing of the session forces closing and frees up the resources.
        session.Dispose();
    }

    /// <summary>
    /// Event fired when SSH session is closed.
    /// </summary>
    protected virtual void OnSshSessionClosed(object? sender, SshSessionClosedEventArgs e)
    {
        var session = (SshClientSession)sender!;
        MaybeStartReconnecting(session, e.Reason, e.Message, e.Exception);
    }

    /// <summary>
    /// Unsubscribe from SSH session events.
    /// </summary>
    protected virtual void UnsubscribeSessionEvents(SshClientSession session)
    {
        Requires.NotNull(session, nameof(session));
        session.Closed -= OnSshSessionClosed;
        session.ReportProgress -= OnProgress;
    }

    /// <summary>
    /// Subscribe to SSH session events.
    /// </summary>
    /// <param name="session"></param>
    protected virtual void SubscribeSessionEvents(SshClientSession session)
    {
        Requires.NotNull(session, nameof(session));
        session.Closed += OnSshSessionClosed;
        session.ReportProgress += OnProgress;
    }

    /// <summary>
    /// Get a user-readable reason for SSH session disconnection, or an empty string.
    /// </summary>
    protected virtual string GetDisconnectReason(
        SshDisconnectReason reason,
        string? message,
        Exception? exception)
        =>
        reason switch
        {
            SshDisconnectReason.None or
            SshDisconnectReason.ByApplication => string.Empty,
            SshDisconnectReason.ConnectionLost => " " + (message ?? exception?.Message ?? "Connection lost."),
            SshDisconnectReason.AuthCancelledByUser or
            SshDisconnectReason.NoMoreAuthMethodsAvailable or
            SshDisconnectReason.HostNotAllowedToConnect or
            SshDisconnectReason.IllegalUserName => " Not authorized.",
            SshDisconnectReason.ServiceNotAvailable => " Service not available.",
            SshDisconnectReason.CompressionError or
            SshDisconnectReason.KeyExchangeFailed or
            SshDisconnectReason.MacError or
            SshDisconnectReason.ProtocolError => " Protocol error.",
            SshDisconnectReason.TooManyConnections =>
                IsClientConnection ? " Too many client connections." : " Another host for the tunnel has connected.",
            _ => string.Empty,
        };

    /// <summary>
    /// Gets the fresh tunnel access token when Relay service returns 401.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If the connection is disposed or disconnected.</exception>
    /// <exception cref="ArgumentException">If current status is not <see cref="ConnectionStatus.Connecting"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the refreshed token is expired.</exception>
    protected virtual async Task<bool> RefreshTunnelAccessTokenAsync(CancellationToken cancellation)
    {
        ConnectionStatus = ConnectionStatus.RefreshingTunnelAccessToken;
        Trace.Verbose(
            "Refreshing tunnel access token. Current token: {0}",
            TunnelAccessTokenProperties.GetTokenTrace(this.accessToken));
        try
        {
            if (!await OnRefreshingTunnelAccessTokenAsync(cancellation))
            {
                return false;
            }

            Trace.Verbose(
                "Refreshed tunnel access token. New token: {0}",
                TunnelAccessTokenProperties.GetTokenTrace(this.accessToken));

            return true;
        }
        finally
        {
            ConnectionStatus = ConnectionStatus.Connecting;
        }
    }

    /// <summary>
    /// Start connecting relay client.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If relay client is disposed.</exception>
    protected virtual void StartConnecting() =>
        ConnectionStatus = ConnectionStatus.Connecting;

    /// <summary>
    /// Finish connecting relay client.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If relay client is disposed.</exception>
    protected virtual void FinishConnecting(SshDisconnectReason reason, Exception? disconnectException)
    {
        lock (DisposeLock)
        {
            if (reason == SshDisconnectReason.None)
            {
                if (ConnectionStatus == ConnectionStatus.Connecting)
                {
                    // If there were temporary connection issue, DisconnectException may be not null.
                    // Since we have successfully connected after all, clean it up.
                    DisconnectReason = SshDisconnectReason.None;
                    DisconnectException = null;
                }

                ConnectionStatus = ConnectionStatus.Connected;
            }
            else if (ConnectionStatus != ConnectionStatus.Disconnected)
            {
                // Do not overwrite disconnect exception and reason if already disconnected.
                DisconnectReason = reason;
                if (disconnectException != null)
                {
                    DisconnectException = disconnectException;
                }

                ConnectionStatus = ConnectionStatus.Disconnected;
            }
        }
    }

    #region IRelayClient

    /// <inheritdoc />
    string IRelayClient.TunnelAccessScope => TunnelAccessScope;

    /// <inheritdoc />
    TraceSource IRelayClient.Trace => Trace;

    /// <inheritdoc />
    string IRelayClient.ConnectionRole => ConnectionRole;

    /// <inheritdoc />
    CancellationToken IRelayClient.DisposeToken => DisposeToken;

    /// <inheritdoc />
    void IRelayClient.StartConnecting() =>
        StartConnecting();

    /// <inheritdoc />
    void IRelayClient.FinishConnecting(SshDisconnectReason reason, Exception? disconnectException) =>
        FinishConnecting(reason, disconnectException);

    /// <inheritdoc />
    Task<Stream> IRelayClient.CreateSessionStreamAsync(CancellationToken cancellation) =>
        CreateSessionStreamAsync(cancellation);

    /// <inheritdoc />
    Task IRelayClient.ConfigureSessionAsync(
        Stream stream,
        bool isReconnect,
        TunnelConnectionOptions? options,
        CancellationToken cancellation) =>
        ConfigureSessionAsync(stream, isReconnect, options, cancellation);

    /// <inheritdoc />
    Task IRelayClient.CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception) =>
        CloseSessionAsync(disconnectReason, exception);

    /// <inheritdoc />
    Task<bool> IRelayClient.RefreshTunnelAccessTokenAsync(CancellationToken cancellation) =>
        RefreshTunnelAccessTokenAsync(cancellation);

    /// <inheritdoc />
    void IRelayClient.OnRetrying(RetryingTunnelConnectionEventArgs e) =>
        OnRetrying(e);

    #endregion

    #region IPortForwardMessageFactory

    Task<PortForwardRequestMessage> IPortForwardMessageFactory.CreateRequestMessageAsync(int port)
        => Task.FromResult<PortForwardRequestMessage>(
            new PortRelayRequestMessage { AccessToken = this.accessToken });

    Task<PortForwardSuccessMessage> IPortForwardMessageFactory.CreateSuccessMessageAsync(int port)
        => Task.FromResult(new PortForwardSuccessMessage()); // Success messages are not extended.

    Task<PortForwardChannelOpenMessage> IPortForwardMessageFactory.CreateChannelOpenMessageAsync(int port)
        => Task.FromResult<PortForwardChannelOpenMessage>(
            new PortRelayConnectRequestMessage
            {
                AccessToken = this.accessToken,
                IsE2EEncryptionRequested = this.EnableE2EEncryption,
            });

    #endregion IPortForwardMessageFactory

    private async Task ReconnectAsync(CancellationToken cancellation)
    {
        Requires.NotNull(this.connector!, nameof(this.connector));

        try
        {
            await this.connector.ConnectSessionAsync(
                this.connectionOptions,
                isReconnect: true,
                cancellation);
        }
        catch (Exception ex)
        {
            if (Tunnel != null)
            {
                var connectFailedEvent = new TunnelEvent($"{ConnectionRole}_reconnect_failed");
                connectFailedEvent.Severity = TunnelEvent.Error;
                connectFailedEvent.Details = ex.ToString();
                ManagementClient?.ReportEvent(Tunnel, connectFailedEvent);
            }

            // Tracing of the exception has already been done by ConnectSessionAsync.
            // As reconnection is an async process, there is nobody watching it throw.
            // The exception, if it was not cancellation, is stored in DisconnectException property.
            // There might have been ConnectionStatusChanged event fired as well.
        }

        lock (DisposeLock)
        {
            this.reconnectTask = null;
        }
    }

    private void OnProgress(object? sender, SshReportProgressEventArgs e)
    {
        this.OnReportProgress(e.Progress, e.SessionNumber);
    }
}
