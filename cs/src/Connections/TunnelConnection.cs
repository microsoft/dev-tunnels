// <copyright file="TunnelConnection.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Base class for tunnel client and host.
/// </summary>
public abstract class TunnelConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource disposeCts = new();
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
    /// Connects to a tunnel.
    /// </summary>
    /// <param name="tunnel">Tunnel to connect to.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The tunnel was not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The client or host does not have
    /// access to connect to the tunnel.</exception>
    /// <exception cref="TunnelConnectionException">The client or host failed to connect to the
    /// tunnel, or connected but encountered a protocol error.</exception>
    public Task ConnectAsync(Tunnel tunnel, CancellationToken cancellation = default)
        => ConnectAsync(tunnel, options: null, cancellation);

    /// <summary>
    /// Connects to a tunnel.
    /// </summary>
    /// <param name="tunnel">Tunnel to connect to.</param>
    /// <param name="options">Options for the connection.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The tunnel was not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The client or host does not have
    /// access to connect to the tunnel.</exception>
    /// <exception cref="TunnelConnectionException">The client or host failed to connect to the
    /// tunnel, or connected but encountered a protocol error.</exception>
    public abstract Task ConnectAsync(
        Tunnel tunnel, TunnelConnectionOptions? options, CancellationToken cancellation = default);

    /// <summary>
    /// Gets the connection status.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// If setting any status but <see cref="ConnectionStatus.Disconnected"/> when connection is disposed.
    /// </exception>
    public ConnectionStatus ConnectionStatus
    {
        get => this.connectionStatus;
        protected set
        {
            lock (DisposeLock)
            {
                if (value != ConnectionStatus.Disconnected &&
                    DisposeToken.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (value == ConnectionStatus.RefreshingTunnelAccessToken &&
                    this.connectionStatus != ConnectionStatus.Connecting)
                {
                    Requires.Fail("Refreshing tunnel access token is allowed only when connecting.");
                }

                var previousConnectionStatus = this.connectionStatus;
                if (value != previousConnectionStatus)
                {
                    this.connectionStatus = value;
                    OnConnectionStatusChanged(previousConnectionStatus, value);
                }
            }
        }
    }

    /// <summary>
    /// Get the last exception that caused disconnection.
    /// <c>null</c> if not yet connected, or connected succesfully, or disposed explicitly.
    /// If not-null, this is the last exception that caused reconnection.
    /// </summary>
    public Exception? DisconnectException
    {
        get;
        protected set;
    }

    /// <summary>
    /// Get the tunnel that is being hosted or connected to.
    /// May be null if the tunnel client used relay service URL and tunnel access token directly.
    /// </summary>
    public Tunnel? Tunnel {
        get => this.tunnel;
        protected set
        {
            if (value != this.tunnel)
            {
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
    /// <remarks>Note: uses the client's system time for validation.</remarks>
    protected virtual void ValidateAccessToken()
    {
        if (!string.IsNullOrEmpty(this.accessToken))
        {
            TunnelAccessTokenProperties.ValidateTokenExpiration(this.accessToken);
        }
    }

    /// <summary>
    /// Get tunnel access scope for this tunnel client or host.
    /// </summary>
    protected abstract string TunnelAccessScope { get; }

    /// <summary>
    /// Get the tunnel connection role, either "client", or "host", depending on <see cref="TunnelAccessScope"/>.
    /// </summary>
    protected string ConnectionRole =>
        IsClientConnection ? "client" : "host";

    /// <summary>
    /// Get a value indicating if this is a client tunnel connection (as opposed to host connection).
    /// </summary>
    protected bool IsClientConnection =>
        TunnelAccessScope == TunnelAccessScopes.Connect;

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
    /// Event raised when a tunnel needs to be refreshed.
    /// </summary>
    public event EventHandler<RefreshingTunnelEventArgs>? RefreshingTunnel;

    /// <summary>
    /// Event raised to report connection progress.
    /// </summary>
    public event EventHandler<TunnelReportProgressEventArgs>? ReportProgress;

    /// <summary>
    /// Connection status changed event.
    /// </summary>
    /// <remarks>
    /// Before any connection attempt is made, the connection status is
    /// <see cref="ConnectionStatus.None"/>.
    ///
    /// The status changes to <see cref="ConnectionStatus.Connecting"/> when a connection attempt
    /// begins.
    ///
    /// The status changes to <see cref="ConnectionStatus.Connected"/> when a connection succeeds.
    ///
    /// When a connection attempt fails without ever connecting, and retries are not enabled
    /// (<see cref="TunnelConnectionOptions.EnableRetry" /> is false) or an unrecoverable error was
    /// encountered, the status changes to <see cref="ConnectionStatus.Disconnected" />.
    ///
    /// When a successful connection is lost, the status changes to either
    /// <see cref="ConnectionStatus.Connecting" /> if reconnect is enabled
    /// (<see cref="TunnelConnectionOptions.EnableReconnect" /> is true or unspecified), otherwise
    /// <see cref="ConnectionStatus.Disconnected" />.
    /// </remarks>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

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

    /// <summary>
    /// Fetch the tunnel from the service if <see cref="ManagementClient"/> and <see cref="Tunnel"/> are not null.
    /// </summary>
    /// <returns><c>true</c> if <see cref="Tunnel"/> was refreshed; otherwise, <c>false</c>.</returns>
    protected virtual async Task<bool> RefreshTunnelAsync(bool includePorts, CancellationToken cancellation)
    {
        var handler = RefreshingTunnel;
        if (handler == null && (Tunnel == null || ManagementClient == null))
        {
            return false;
        }

        Trace.TraceInformation("Refreshing tunnel{0}", includePorts ? " and ports." : ".");
        var isRefreshed = false;
        if (handler != null)
        {
            var e = new RefreshingTunnelEventArgs(
                TunnelAccessScope,
                Tunnel,
                ManagementClient,
                includePorts,
                cancellation);

            handler(this, e);
            if (e.TunnelRefreshTask != null)
            {
                Tunnel = await e.TunnelRefreshTask;
                isRefreshed = true;
            }
        }

        if (!isRefreshed && Tunnel != null && ManagementClient != null)
        {
            var options = new TunnelRequestOptions
            {
                TokenScopes = new[] { TunnelAccessScope },
                IncludePorts = includePorts,
            };

            Tunnel = await ManagementClient.GetTunnelAsync(Tunnel, options, cancellation);
            isRefreshed = true;
        }

        if (isRefreshed)
        {
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
            return await RefreshTunnelAsync(includePorts: false, cancellation);
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

    /// <summary>
    /// Event raised when a keep-alive message response is not received.
    /// </summary>
    protected virtual void OnKeepAliveFailed(int count)
    {
        KeepAliveFailed?.Invoke(this, new SshKeepAliveEventArgs(count));
    }

    /// <summary>
    /// Event raised when a keep-alive message response is not received.
    /// </summary>
    protected virtual void OnKeepAliveSucceeded(int count)
    {
        KeepAliveSucceeded?.Invoke(this, new SshKeepAliveEventArgs(count));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (DisposeLock)
        {
            if (this.disposeCts.IsCancellationRequested)
            {
                return;
            }

            this.disposeCts.Cancel();
        }

        try
        {
            await DisposeConnectionAsync();
        }
        catch (Exception exception)
        {
            Trace.Error("Error disposing {0}: {1}", ConnectionRole, exception);
        }
        finally
        {
            ConnectionStatus = ConnectionStatus.Disconnected;
        }
    }

    /// <summary>
    /// Close tunnel connection and dispose it.
    /// </summary>
    /// <returns></returns>
    protected virtual Task DisposeConnectionAsync() => Task.CompletedTask;

    /// <summary>
    /// Event fired when the connection status has changed.
    /// </summary>
    protected virtual void OnConnectionStatusChanged(
        ConnectionStatus previousConnectionStatus,
        ConnectionStatus connectionStatus)
    {
        var handler = ConnectionStatusChanged;
        if (handler != null)
        {
            // Disconnect exception is provided only during disconnection, not disposal.
            var disconnectException =
                connectionStatus == ConnectionStatus.Disconnected && !DisposeToken.IsCancellationRequested ?
                DisconnectException : null;

            var args = new ConnectionStatusChangedEventArgs(
                previousConnectionStatus,
                connectionStatus,
                disconnectException);

            handler(this, args);
        }
    }

    /// <summary>
    /// Notifies about a connection retry, giving the application a chance to delay or cancel it.
    /// </summary>
    internal void OnRetrying(RetryingTunnelConnectionEventArgs e)
    {
        if (e.Retry)
        {
            RetryingTunnelConnection?.Invoke(this, e);
        }

        if (Tunnel != null)
        {
            var retryingEvent = new TunnelEvent($"{ConnectionRole}_connect_retrying");
            retryingEvent.Severity = TunnelEvent.Warning;
            retryingEvent.Details = e.Exception?.ToString();
            retryingEvent.Properties = new Dictionary<string, string>
            {
                [nameof(e.Retry)] = e.Retry.ToString(),
                [nameof(e.AttemptNumber)] = e.AttemptNumber.ToString(),
                [nameof(e.Delay)] = ((int)e.Delay.TotalMilliseconds).ToString(),
            };
            ManagementClient?.ReportEvent(Tunnel, retryingEvent);
        }
    }

    /// <summary>
    /// Event fired when a SSH progress connection event has been reported.
    /// </summary>
    protected virtual void OnReportProgress(Progress progress, int? sessionNumber = null)
    {
        if (ReportProgress is EventHandler<TunnelReportProgressEventArgs> handler)
        {
            var args = new TunnelReportProgressEventArgs(progress.ToString(), sessionNumber);
            ReportProgress.Invoke(this, args);
        }
    }

    /// <summary>
    /// Event fired when a tunnel progress event has been reported.
    /// </summary>
    protected virtual void OnReportProgress(TunnelProgress progress)
    {
        if (ReportProgress is EventHandler<TunnelReportProgressEventArgs> handler)
        {
            var args = new TunnelReportProgressEventArgs(progress.ToString());
            ReportProgress.Invoke(this, args);
        }
    }
}
