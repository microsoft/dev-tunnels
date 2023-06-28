// <copyright file="TunnelConnection.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Connections.Messages;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Base class for tunnel client and host.
/// </summary>
public abstract class TunnelConnection : IAsyncDisposable, IPortForwardMessageFactory
{
    private readonly CancellationTokenSource disposeCts = new();
    private Task? reconnectTask;
    private ConnectionStatus connectionStatus;
    private Tunnel? tunnel;

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelConnection"/> class.
    /// </summary>
    public TunnelConnection(ITunnelManagementClient? managementClient, TraceSource trace)
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
            lock (DisposeLock)
            {
                if (this.disposeCts.IsCancellationRequested)
                {
                    value = ConnectionStatus.Disconnected;
                }

                var previousConnectionStatus = this.connectionStatus;
                this.connectionStatus = value;
                if (value != previousConnectionStatus)
                {
                    // If there were temporary connection issue, DisconnectException may be not null.
                    // Since we have successfuly connected after all, clean it up.
                    if (value == ConnectionStatus.Connected)
                    {
                        DisconnectException = null;
                    }

                    OnConnectionStatusChanged(previousConnectionStatus, value);
                }
            }
        }
    }

    /// <summary>
    /// Get the last exception that caused disconnection.
    /// Null if not yet connected.
    /// If disconnection was caused by disposing of this object, the value may be either null, or the last exception when the connection failed.
    /// </summary>
    public Exception? DisconnectException { get; private set; }

    /// <summary>
    /// Get the tunnel that is being hosted or connected to.
    /// May be null if the tunnel client used relay service URL and tunnel access token directly.
    /// </summary>
    public Tunnel? Tunnel {
        get => this.tunnel;
        private set
        {
            if (value != this.tunnel)
            {
                // Get the tunnel access token from the new tunnel, or the original Tunnal object if the new tunnel doesn't have the token,
                // which may happen when the tunnel was authenticated with a tunnel access token from Tunnel.AccessTokens.
                // Add the tunnel access token to the new tunnel's AccessTokens if it is not there.
                string? accessToken;
                if (value != null &&
                    !value.TryGetAccessToken(TunnelAccessScope, out var _) &&
                    this.tunnel?.TryGetAccessToken(TunnelAccessScope, out accessToken) == true &&
                    !string.IsNullOrEmpty(accessToken) &&
                    TunnelAccessTokenProperties.TryParse(accessToken) is TunnelAccessTokenProperties tokenProperties &&
                    (tokenProperties.Expiration == null || tokenProperties.Expiration > DateTime.UtcNow))
                {
                    value.AccessTokens ??= new Dictionary<string, string>();
                    value.AccessTokens[TunnelAccessScope] = accessToken;
                }

                this.tunnel = value;
                OnTunnelChanged();
            }
        }
    }

    /// <summary>
    /// Trace to write output to console.
    /// </summary>
    protected TraceSource Trace { get; }

    /// <summary>
    /// Dispose token.
    /// </summary>
    protected CancellationToken DisposeToken => this.disposeCts.Token;

    /// <summary>
    /// Lock object that guards disposal.
    /// </summary>
    /// <remarks>
    /// Locking on <see cref="DisposeLock"/> guarantees that <see cref="DisposeToken"/> won't get cancelled while the lock is held.
    /// </remarks>
    protected object DisposeLock { get; } = new();

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
    /// Determines whether E2E encryption is requested when opening connections through the tunnel
    /// (V2 protocol only).
    /// </summary>
    /// <remarks>
    /// The default value is true, but applications may set this to false (for slightly faster
    /// connections).
    /// <para/>
    /// Note when this is true, E2E encryption is not strictly required. The tunnel relay and
    /// tunnel host can decide whether or not to enable E2E encryption for each connection,
    /// depending on policies and capabilities. Applications can verify the status of E2EE by
    /// handling the <see cref="ITunnelClient.ForwardedPortConnecting" /> or
    /// <see cref="ITunnelHost.ForwardedPortConnecting" /> event and checking the related property
    /// on the channel request or response message.
    /// </remarks>
    public bool EnableE2EEncryption { get; set; } = true;

    /// <summary>
    /// Tunnel has been assigned to or changed.
    /// </summary>
    protected virtual void OnTunnelChanged()
    {
        this.accessToken = Tunnel?.TryGetAccessToken(TunnelAccessScope, out var token) == true ? token : null;
    }

    /// <summary>
    /// Validate <see cref="accessToken"/> if it is not null or empty.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">is thrown if the <see cref="accessToken"/> is expired.</exception>
    protected virtual void ValidateAccessToken()
    {
        if (!string.IsNullOrEmpty(this.accessToken))
        {
            TunnelAccessTokenProperties.ValidateTokenExpiration(this.accessToken);
        }
    }

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
    public event EventHandler<RefreshingTunnelAccessTokenEventArgs>? RefreshingTunnelAccessToken;

    /// <summary>
    /// Event raised when a tunnel connection attempt failed and is about to be retried.
    /// </summary>
    /// <remarks>
    /// An event handler can cancel the retry by setting <see cref="RetryingTunnelConnectionEventArgs.Retry"/> to false.
    /// </remarks>
    public event EventHandler<RetryingTunnelConnectionEventArgs>? RetryingTunnelConnection;

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
    /// Close tunnel session with the given reason and exception.
    /// </summary>
    /// <remarks>
    /// This is used by <see cref="ITunnelConnector"/> when it couldn't connect the tunnel SSH session.
    /// Depending on whether the exception is recoverable, the tunnel connector may try to reconnect and start a new session,
    /// or give up and change <see cref="ConnectionStatus"/> to <see cref="ConnectionStatus.Disconnected"/>.
    /// </remarks>
    protected virtual Task CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception)
    {
        if (exception != null)
        {
            DisconnectException = exception;
        }

        return Task.CompletedTask;
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
    /// <exception cref="UnauthorizedAccessException">Thown if the refreshed token is expred.</exception>
    protected async Task<bool> RefreshTunnelAccessTokenAsync(CancellationToken cancellation)
    {
        var previousStatus = ConnectionStatus;
        ConnectionStatus = ConnectionStatus.RefreshingTunnelAccessToken;
        Trace.TraceInformation(
            "Refreshing tunnel access token. Current token: {0}",
            TunnelAccessTokenProperties.GetTokenTrace(this.accessToken));
        try
        {
            if (!await OnRefreshingTunnelAccessTokenAsync(cancellation))
            {
                return false;
            }

            // Access token may be null if tunnel allows anonymous access.
            if (this.accessToken != null)
            {
                TunnelAccessTokenProperties.ValidateTokenExpiration(this.accessToken);
            }

            Trace.TraceInformation(
                "Refreshed tunnel access token. New token: {0}",
                TunnelAccessTokenProperties.GetTokenTrace(this.accessToken));

            return true;
        }
        finally
        {
            ConnectionStatus = previousStatus;
        }
    }

    /// <summary>
    /// Fetch the tunnel from the service if <see cref="ManagementClient"/> and <see cref="Tunnel"/> are not null.
    /// </summary>
    /// <returns><c>true</c> if <see cref="Tunnel"/> was refreshed; otherwise, <c>false</c>.</returns>
    protected virtual async Task<bool> RefreshTunnelAsync(CancellationToken cancellation)
    {
        if (Tunnel != null && ManagementClient != null)
        {
            Trace.TraceInformation("Refreshing tunnel.");
            var options = new TunnelRequestOptions
            {
                TokenScopes = new[] { TunnelAccessScope },
            };

            Tunnel = await ManagementClient.GetTunnelAsync(Tunnel, options, cancellation);
            if (Tunnel != null)
            {
                Trace.TraceInformation("Refreshed tunnel.");
            }
            else
            {
                Trace.TraceInformation("Tunnel not found.");
            }

            return true;
        }

        return false;
    }


    /// <summary>
    /// Refresh tunnel access token.
    /// </summary>
    /// <remarks>
    /// If <see cref="Tunnel"/>, <see cref="ManagementClient"/> are not null and <see cref="RefreshingTunnelAccessToken"/> is null,
    /// refreshes the tunnel with <see cref="ITunnelManagementClient.GetTunnelAsync(Tunnel, TunnelRequestOptions?, CancellationToken)"/>
    /// and this gets the token off it based on <see cref="TunnelAccessScope"/>.
    /// Otherwise, invokes <see cref="RefreshingTunnelAccessToken"/> event.
    /// </remarks>
    protected virtual async Task<bool> OnRefreshingTunnelAccessTokenAsync(CancellationToken cancellation)
    {
        var handler = RefreshingTunnelAccessToken;
        if (handler == null)
        {
            return await RefreshTunnelAsync(cancellation);
        }

        var eventArgs = new RefreshingTunnelAccessTokenEventArgs(TunnelAccessScope, cancellation);
        handler(this, eventArgs);
        if (eventArgs.TunnelAccessTokenTask == null)
        {
            return false;
        }

        this.accessToken = await eventArgs.TunnelAccessTokenTask.ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        Task? reconnectTask;
        lock (DisposeLock)
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
        lock (DisposeLock)
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

        lock (DisposeLock)
        {
            this.reconnectTask = null;
        }
    }

    /// <summary>
    /// Notifies about a connection retry, giving the application a chance to delay or cancel it.
    /// </summary>
    internal void OnRetrying(RetryingTunnelConnectionEventArgs e)
    {
        RetryingTunnelConnection?.Invoke(this, e);
    }

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
}
