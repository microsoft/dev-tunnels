// <copyright file="SshPfsClientBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh;
using Microsoft.VisualStudio.Ssh.Events;
using Microsoft.VisualStudio.Ssh.Messages;
using Microsoft.VisualStudio.Ssh.Tcp;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Base class for clients that connect with SSH Port Forwarding Service
/// </summary>
public class SshPfsClientBase : IAsyncDisposable
{
    private bool acceptLocalConnectionsForForwardedPorts = true;

    /// <summary>
    /// Creates a new instance of the <see cref="SshPfsClientBase" /> class.
    /// </summary>
    public SshPfsClientBase(
        TraceSource trace)
    {
        Trace = Requires.NotNull(trace, nameof(trace));
    }

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
    /// Session used to connect to host
    /// </summary>
    protected SshClientSession? SshSession { get; set; }

    /// <summary>
    /// Port forwarding service on <see cref="SshSession"/>.
    /// </summary>
    protected PortForwardingService? SshPortForwardingService { get; private set; }

    /// <summary>
    /// Trace to write output to console
    /// </summary>
    protected TraceSource Trace { get; }

    /// <summary>
    /// A value indicating whether the SSH session is active.
    /// </summary>
    protected bool IsSshSessionActive { get; private set; }

    private void OnRequest(object? sender, SshRequestEventArgs<SessionRequestMessage> e)
    {
        if (e.Request.RequestType == "tcpip-forward" ||
            e.Request.RequestType == "cancel-tcpip-forward")
        {
            // SshPortForwardingService.AcceptLocalConnectionsForForwardedPorts may be set to disable listening on local TCP ports
            e.IsAuthorized = true;
        }
    }

    /// <summary>
    /// Start Ssh session on the <paramref name="stream"/>.
    /// </summary>
    /// <remarks>
    /// Overwrites <see cref="SshSession"/> property.
    /// </remarks>
    protected async Task StartSshSessionAsync(Stream stream, CancellationToken cancellation)
    {
        var clientConfig = new SshSessionConfiguration();

        // Enable port-forwarding via the SSH protocol.
        clientConfig.AddService(typeof(PortForwardingService));

        this.SshSession = new SshClientSession(clientConfig, Trace.WithName("SSH"));
        this.SshSession.Authenticating += OnSshServerAuthenticating;
        SshPortForwardingService = this.SshSession.ActivateService<PortForwardingService>();
        ConfigurePortForwardingService();
        this.SshSession.Request += OnRequest;

        SshSessionCreated();
        await this.SshSession.ConnectAsync(stream, cancellation);

        // For now, the client is allowed to skip SSH authentication;
        // they must have a valid tunnel access token already to get this far.
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await this.SshSession.AuthenticateAsync(clientCredentials);
    }

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

    /// <summary>
    /// SSH session has just closed.
    /// </summary>
    protected virtual void OnSshSessionClosed(Exception? exception)
    {
        IsSshSessionActive = false;
    }

    private void OnSshServerAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        // TODO: Validate host public keys match those published to the service?
        // For now, the assumption is only a host with access to the tunnel can get a token
        // that enables listening for tunnel connections.
        e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.SshSession != null)
        {
            await this.SshSession.CloseAsync(SshDisconnectReason.ByApplication);
        }
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
    }
}
