// <copyright file="TunnelClientBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh;
using Microsoft.VisualStudio.Ssh.Events;
using Microsoft.VisualStudio.Ssh.Messages;
using Microsoft.VisualStudio.Ssh.Tcp;
using Microsoft.VisualStudio.Ssh.Tcp.Events;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Base class for clients that connect to a single host
/// </summary>
public abstract class TunnelClientBase : TunnelBase, ITunnelClient
{
    private bool acceptLocalConnectionsForForwardedPorts = true;

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelClientBase" /> class.
    /// </summary>
    public TunnelClientBase(ITunnelManagementClient? managementClient, TraceSource trace) : base(managementClient, trace)
    {
    }

    /// <inheritdoc />
    public abstract IReadOnlyCollection<TunnelConnectionMode> ConnectionModes { get; }

    /// <inheritdoc />
    public ForwardedPortsCollection? ForwardedPorts =>
        SshPortForwardingService?.RemoteForwardedPorts;

    /// <summary>
    /// Session used to connect to host
    /// </summary>
    protected SshClientSession? SshSession { get; set; }

    /// <summary>
    /// Port forwarding service on <see cref="SshSession"/>.
    /// </summary>
    protected PortForwardingService? SshPortForwardingService { get; private set; }

    /// <summary>
    /// A value indicating whether the SSH session is active.
    /// </summary>
    protected bool IsSshSessionActive { get; private set; }

    /// <inheritdoc />
    protected override string TunnelAccessScope => TunnelAccessScopes.Connect;

    /// <summary>
    /// Get a value indicating if remote <paramref name="port"/> is forwarded and has any channels open on the client,
    /// whether used by local tcp listener if <see cref="AcceptLocalConnectionsForForwardedPorts"/> is true, or
    /// streamed via <see cref="ConnectToForwardedPortAsync(int, CancellationToken)"/>.
    /// </summary>
    protected bool HasForwardedChannels(int port) =>
        IsSshSessionActive &&
        SshPortForwardingService!.RemoteForwardedPorts.FirstOrDefault(p => p.RemotePort == port) is ForwardedPort forwardedPort &&
        SshPortForwardingService!.RemoteForwardedPorts.GetChannels(forwardedPort).Any();

    /// <summary>
    /// SSH session closed event.
    /// The client may try to reconnect after firing this event.
    /// </summary>
    protected EventHandler? SshSessionClosed { get; set; }

    /// <summary>
    ///  A value indicating whether local connections for forwarded ports are accepted.
    /// </summary>
    public bool AcceptLocalConnectionsForForwardedPorts
    {
        get => this.acceptLocalConnectionsForForwardedPorts;
        set
        {
            if (value != this.acceptLocalConnectionsForForwardedPorts)
            {
                this.acceptLocalConnectionsForForwardedPorts = value;
                ConfigurePortForwardingService();
            }
        }
    }

    /// <summary>
    /// Get host Id the client is connecting to.
    /// </summary>
    public string? HostId { get; private set; }

    /// <inheritdoc />
    public async Task ConnectAsync(Tunnel tunnel, string? hostId, CancellationToken cancellation)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        Requires.NotNull(tunnel.Endpoints!, nameof(Tunnel.Endpoints));

        if (this.SshSession != null)
        {
            throw new InvalidOperationException(
                "Already connected. Use separate instances to connect to multiple tunnels.");
        }

        if (tunnel.Endpoints.Length == 0)
        {
            throw new InvalidOperationException(
                "No hosts are currently accepting connections for the tunnel.");
        }

        HostId = hostId;
        await ConnectTunnelSessionAsync(tunnel, cancellation);
    }

    /// <inheritdoc />
    public Task WaitForForwardedPortAsync(int forwardedPort, CancellationToken cancellation) =>
        SshPortForwardingService is PortForwardingService pfs ?
        pfs.WaitForForwardedPortAsync(forwardedPort, cancellation) :
        throw new InvalidOperationException("Port forwarding has not been started. Ensure that the client has connected by calling ConnectAsync.");

    /// <summary>
    /// Start SSH session on the <paramref name="stream"/>.
    /// </summary>
    /// <remarks>
    /// Overwrites <see cref="SshSession"/> property.
    /// SSH session reconnect is enabled only if <see cref="TunnelBase.connector"/> is not null.
    /// </remarks>
    protected async Task StartSshSessionAsync(Stream stream, CancellationToken cancellation)
    {
        ConnectionStatus = ConnectionStatus.Connecting;
        if (this.SshSession != null)
        {
            // Unsubscribe event handler from the previous session.
            this.SshSession.Authenticating -= OnSshServerAuthenticating;
            this.SshSession.Disconnected -= OnSshSessionDisconnected;
            this.SshSession.Closed -= SshSession_Closed;
            this.SshSession.Request -= OnRequest;
        }

        // Enable reconnect only if connector is set as reconnect depends on it.
        var clientConfig = new SshSessionConfiguration(enableReconnect: this.connector != null);

        // Enable port-forwarding via the SSH protocol.
        clientConfig.AddService(typeof(PortForwardingService));

        this.SshSession = new SshClientSession(clientConfig, Trace.WithName("SSH"));
        this.SshSession.Authenticating += OnSshServerAuthenticating;
        this.SshSession.Disconnected += OnSshSessionDisconnected;

        SshPortForwardingService = this.SshSession.ActivateService<PortForwardingService>();
        ConfigurePortForwardingService();
        this.SshSession.Request += OnRequest;

        SshSessionCreated();
        await this.SshSession.ConnectAsync(stream, cancellation);

        // For now, the client is allowed to skip SSH authentication;
        // they must have a valid tunnel access token already to get this far.
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await this.SshSession.AuthenticateAsync(clientCredentials);
        ConnectionStatus = ConnectionStatus.Connected;
    }

    private void OnSshSessionDisconnected(object? sender, EventArgs e) =>
        StartReconnectTaskIfNotDisposed();

    private void ConfigurePortForwardingService()
    {
        if (SshPortForwardingService is PortForwardingService pfs)
        {
            pfs.AcceptLocalConnectionsForForwardedPorts = this.acceptLocalConnectionsForForwardedPorts;
            if (pfs.AcceptLocalConnectionsForForwardedPorts)
            {
                pfs.TcpListenerFactory = new RetryTcpListenerFactory();
            }
        }
    }

    /// <summary>
    /// Ssh session has just been created but has not connected yet.
    /// This is a good place to set up event handlers and activate services on it.
    /// </summary>
    protected virtual void SshSessionCreated()
    {
        // All tunnel hosts and clients should disable this because they do not use it (for now) and leaving it enabled is a potential security issue.
        SshPortForwardingService!.AcceptRemoteConnectionsForNonForwardedPorts = false;
        SshSession!.Closed += SshSession_Closed;
        IsSshSessionActive = true;
    }

    /// <inheritdoc />
    protected override async Task CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception)
    {
        await base.CloseSessionAsync(disconnectReason, exception);
        if (SshSession != null && !SshSession.IsClosed)
        {
            if (exception != null)
            {
                await SshSession.CloseAsync(disconnectReason, exception);
            }
            else
            {
                await SshSession.CloseAsync(disconnectReason);
            }

            // Closing the SSH session does nothing if the session is in disconnected state,
            // which may happen for a reconnectable session when the connection drops.
            // Disposing of the session forces closing and frees up the resources.
            SshSession.Dispose();
        }
    }

    /// <summary>
    /// SSH session has just closed.
    /// </summary>
    protected virtual void OnSshSessionClosed(Exception? exception)
    {
        IsSshSessionActive = false;
        SshSessionClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSshServerAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        // TODO: Validate host public keys match those published to the service?
        // For now, the assumption is only a host with access to the tunnel can get a token
        // that enables listening for tunnel connections.
        e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
    }

    private void OnRequest(object? sender, SshRequestEventArgs<SessionRequestMessage> e)
    {
        if (e.Request.RequestType == "tcpip-forward" ||
            e.Request.RequestType == "cancel-tcpip-forward")
        {
            // SshPortForwardingService.AcceptLocalConnectionsForForwardedPorts may be set to disable listening on local TCP ports
            e.IsAuthorized = true;
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (this.SshSession != null)
        {
            await this.SshSession.CloseAsync(SshDisconnectReason.ByApplication);
            this.SshSession.Dispose();
        }

        SshSessionClosed = null;
    }

    /// <summary>
    /// Opens a stream connected to a remote port for clients which cannot or do not want to forward local TCP ports.
    /// Returns null if the session gets closed, or the port is no longer forwarded by the host.
    /// </summary>
    /// <remarks>
    /// Set <see cref="AcceptLocalConnectionsForForwardedPorts"/> to <c>false</c> before connecting the client to ensure
    /// that forwarded tunnel ports won't get local TCP listeners.
    /// </remarks>
    /// <param name="forwardedPort">Remote port to connect to.</param>
    /// <param name="cancellation">Cancellation token for the request.</param>
    /// <returns>A <see cref="Task{Stream}"/> representing the result of the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">If the tunnel is not yet connected and hasn't started connecting.</exception>
    public virtual async Task<Stream?> ConnectToForwardedPortAsync(int forwardedPort, CancellationToken cancellation)
    {
        if (!(SshPortForwardingService is PortForwardingService pfs))
        {
            throw new InvalidOperationException("The client is not connected yet.");
        }

        try
        {
            return await pfs.ConnectToForwardedPortAsync(forwardedPort, cancellation);
        }
        catch (InvalidOperationException)
        {
            // The requested port is not forwarded now, though it used to be forwarded.
            // Assume the host has stopped forwarding on that port.
        }
        catch (SshChannelException)
        {
            // The streaming channel could not be opened, either because it was rejected by
            // the remote side, or the remote connection failed.
        }
        catch when (!IsSshSessionActive)
        {
            // The SSH session has closed while we tried to connect to the port. Assume the host has closed it.
            // If the SSH session is still active, and we got some unexpected exception, bubble it up.
        }

        return null;
    }

    private void SshSession_Closed(object? sender, SshSessionClosedEventArgs e)
    {
        if (sender is SshSession sshSession)
        {
            sshSession.Closed -= SshSession_Closed;
        }

        OnSshSessionClosed(e.Exception);
        if (e.Reason == SshDisconnectReason.ConnectionLost)
        {
            StartReconnectTaskIfNotDisposed();
        }
    }
}
