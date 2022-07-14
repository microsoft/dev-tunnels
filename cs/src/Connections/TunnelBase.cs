﻿// <copyright file="TunnelBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Base class for tunnel client and host.
/// </summary>
public abstract class TunnelBase : IAsyncDisposable
{
    private readonly object reconnectLock = new();
    private readonly CancellationTokenSource disposeCts = new();
    private Task? reconnectTask;
    private ConnectionStatus connectionStatus;

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelBase"/> class.
    /// </summary>
    public TunnelBase(ITunnelManagementClient? managementClient, TraceSource trace)
    {
        ManagementClient = managementClient;
        Trace = Requires.NotNull(trace, nameof(trace));
    }

    /// <summary>
    /// Gets the connection status.
    /// </summary>
    public ConnectionStatus ConnectionStatus 
    {
        get => this.connectionStatus;
        protected set
        {
            lock (this.reconnectLock)
            {
                if (this.disposeCts.IsCancellationRequested)
                {
                    value = ConnectionStatus.Disconnected;
                }

                var previousConnectionStatus = this.connectionStatus;
                this.connectionStatus = value;
                if (value != previousConnectionStatus)
                {
                    OnConnectionStatusChanged(previousConnectionStatus, value);
                }
            }
        }
    }

    /// <summary>
    /// Get the exception that caused disconnection.
    /// Null if not yet connected or disconnection was caused by disposing of this object.
    /// </summary>
    public Exception? DisconnectException { get; protected set; }

    /// <summary>
    /// Get the tunnel that is being hosted or connected to.
    /// May be null if the tunnel client used relay service URL and tunnel access token directly.
    /// </summary>
    public Tunnel? Tunnel { get; private set; }

    /// <summary>
    /// Trace to write output to console.
    /// </summary>
    protected TraceSource Trace { get; }

    /// <summary>
    /// Management client used for connections.
    /// Not null for the tunnel host. Maybe null for the tunnel client.
    /// </summary>
    protected ITunnelManagementClient? ManagementClient { get; }

    /// <summary>
    /// Tunnel connector.
    /// </summary>
    protected ITunnelConnector? connector;

    /// <summary>
    /// Tunnel access token.
    /// </summary>
    protected string? accessToken;

    /// <summary>
    /// Create tunnel connector for <see cref="Tunnel"/>.
    /// </summary>
    protected abstract Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation);

    /// <summary>
    /// Get tunnel access scope for this tunnel client or host.
    /// </summary>
    protected abstract string TunnelAccessScope { get; }

    /// <summary>
    /// Event handler for refreshing the tunnel access token.
    /// The tunnel client or host fires this event when it is not able to use the access token it got from the tunnel.
    /// </summary>
    public EventHandler<RefreshingTunnelAccessTokenEventArgs>? RefreshingTunnelAccessToken { get; set; }

    /// <summary>
    /// Connection status changed event.
    /// </summary>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Assign the tunnel and connect to it.
    /// </summary>
    protected Task ConnectTunnelSessionAsync(Tunnel tunnel, CancellationToken cancellation)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        return ConnectTunnelSessionAsync(async (cancellation) =>
        {
            var isReconnect = Tunnel != null;
            Tunnel = tunnel;
            this.connector ??= await CreateTunnelConnectorAsync(cancellation);
            await this.connector.ConnectSessionAsync(isReconnect, cancellation);
        },
        cancellation);
    }

    /// <summary>
    /// Connect to tunnel session by running <paramref name="connectAction"/>.
    /// </summary>
    protected async Task ConnectTunnelSessionAsync(Func<CancellationToken, Task> connectAction, CancellationToken cancellation)
    {
        Requires.NotNull(connectAction, nameof(connectAction));
        ConnectionStatus = ConnectionStatus.Connecting;
        try
        {
            await connectAction(cancellation);
            ConnectionStatus = ConnectionStatus.Connected;
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus = ConnectionStatus.Disconnected;
            throw;
        }
        catch (Exception ex)
        {
            Trace.Error(
                "Error connecting {0} tunnel session: {1}",
                TunnelAccessScope == TunnelAccessScopes.Connect ? "client" : "host",
                ex is UnauthorizedAccessException || ex is TunnelConnectionException ? ex.Message : ex);

            DisconnectException = ex;
            ConnectionStatus = ConnectionStatus.Disconnected;
            throw;
        }
    }

    /// <summary>
    /// Gets the fresh tunnel access token when Relay service returns 401.
    /// </summary>
    protected async Task<bool> RefreshTunnelAccessTokenAsync(CancellationToken cancellation)
    {
        var previousStatus = ConnectionStatus;
        ConnectionStatus = ConnectionStatus.RefreshingTunnelAccessToken;
        try
        {
            var newTunnelAccessToken = await GetFreshTunnelAccessTokenAsync(cancellation);
            if (!string.IsNullOrEmpty(newTunnelAccessToken) &&
                newTunnelAccessToken != this.accessToken)
            {
                this.accessToken = newTunnelAccessToken;
                return true;
            }

            return false;
        }
        finally
        {
            ConnectionStatus = previousStatus;
        }
    }

    /// <summary>
    /// Gets the fresh tunnel access token or null if it cannot.
    /// </summary>
    /// <remarks>
    /// If <see cref="Tunnel"/>, <see cref="ManagementClient"/> are not null and <see cref="RefreshingTunnelAccessToken"/> is null,
    /// gets the tunnel with <see cref="ITunnelManagementClient.GetTunnelAsync(Tunnel, TunnelRequestOptions?, CancellationToken)"/> and gets the token 
    /// off it based on <see cref="TunnelAccessScope"/>.
    /// Otherwise, invokes <see cref="RefreshingTunnelAccessToken"/> event.
    /// </remarks>
    protected virtual async Task<string?> GetFreshTunnelAccessTokenAsync(CancellationToken cancellation)
    {
        if (Tunnel != null &&
            ManagementClient != null &&
            RefreshingTunnelAccessToken == null)
        {
            var options = new TunnelRequestOptions
            {
                TokenScopes = new[] { TunnelAccessScope },
            };

            Tunnel = await ManagementClient.GetTunnelAsync(Tunnel!, options, cancellation);
            return Tunnel!.AccessTokens?.TryGetValue(TunnelAccessScope, out var result) == true ? result : null;
        }

        var eventArgs = new RefreshingTunnelAccessTokenEventArgs(TunnelAccessScope);
        RefreshingTunnelAccessToken?.Invoke(this, eventArgs);

        return eventArgs.TunnelAccessToken ??
            (eventArgs.TunnelAccessTokenProvider != null ?
            await eventArgs.TunnelAccessTokenProvider(TunnelAccessScope, cancellation).ConfigureAwait(false) :
            null);
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        Task? reconnectTask;
        lock (this.reconnectLock)
        {
            this.disposeCts.Cancel();
            reconnectTask = this.reconnectTask;
        }

        if (reconnectTask != null)
        {
            await reconnectTask;
        }

        ConnectionStatus = ConnectionStatus.Disconnected;
    }

    /// <summary>
    /// Event fired when the connection status has changed.
    /// </summary>
    protected virtual void OnConnectionStatusChanged(ConnectionStatus previousConnectionStatus, ConnectionStatus connectionStatus)
    {
        var handler = ConnectionStatusChanged;
        if (handler != null)
        {
            var args = new ConnectionStatusChangedEventArgs(
                previousConnectionStatus, 
                connectionStatus, 
                connectionStatus == ConnectionStatus.Disconnected ? DisconnectException : null);

            handler(this, args);
        }
    }

    /// <summary>
    /// Start reconnect task if the object is not yet disposed.
    /// </summary>
    protected void StartReconnectTaskIfNotDisposed()
    {
        lock (this.reconnectLock)
        {
            if (!this.disposeCts.IsCancellationRequested && 
                this.reconnectTask == null &&
                this.connector != null) // The connector may be null if the tunnel client/host was created directly from a stream.
            {
                var task = ReconnectAsync(this.disposeCts.Token);
                this.reconnectTask = !task.IsCompleted ? task : null;
            }
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellation)
    {
        Requires.NotNull(this.connector!, nameof(this.connector));

        try
        {
            await ConnectTunnelSessionAsync(
                (cancellation) => this.connector.ConnectSessionAsync(isReconnect: true, cancellation),
                cancellation);
        }
        catch
        {
            // Tracing of the exception has already been done by ConnectToTunnelSessionAsync.
            // As reconnection is an async process, there is nobody watching it throw.
            // The exception, if it was not cancellation, is stored in DisconnectException property.
            // There might have been ConnectionStatusChanged event fired as well.
        }

        lock (this.reconnectLock)
        {
            this.reconnectTask = null;
        }
    }
}
