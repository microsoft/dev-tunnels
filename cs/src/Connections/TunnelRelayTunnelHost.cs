// <copyright file="TunnelRelayTunnelHost.cs" company="Microsoft">
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
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Tunnel host implementation that uses data-plane relay
/// to accept client connections.
/// </summary>
public class TunnelRelayTunnelHost : TunnelHostBase
{
    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint.
    /// </summary>
    public const string WebSocketSubProtocol = "tunnel-relay-host";

    /// <summary>
    /// Ssh channel type in host relay ssh session where client session streams are passed.
    /// </summary>
    public const string ClientStreamChannelType = "client-ssh-session-stream";

    private readonly CancellationTokenSource disposeCts = new CancellationTokenSource();
    private readonly IList<Task> clientSessionTasks = new List<Task>();
    private readonly string hostId;

    private MultiChannelStream? hostSession;

    /// <summary>
    /// Creates a new instance of a host that connects to a tunnel via a tunnel relay.
    /// </summary>
    public TunnelRelayTunnelHost(ITunnelManagementClient managementClient, TraceSource trace)
        : base(managementClient, trace)
    {
        this.hostId = MultiModeTunnelHost.HostId;
    }

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
    public override async ValueTask DisposeAsync()
    {
        this.disposeCts.Cancel();

        var hostSession = this.hostSession;
        if (hostSession != null)
        {
            this.hostSession = null;
            await hostSession.CloseAsync();
        }

        List<Task> tasks;
        lock (this.clientSessionTasks)
        {
            tasks = new List<Task>(this.clientSessionTasks);
            this.clientSessionTasks.Clear();
        }

        if (Tunnel != null)
        {
            tasks.Add(ManagementClient.DeleteTunnelEndpointsAsync(Tunnel, this.hostId, TunnelConnectionMode.TunnelRelay));
        }

        foreach (RemotePortForwarder forwarder in RemoteForwarders.Values)
        {
            forwarder.Dispose();
        }

        while (this.SshSessions.TryTake(out var sshSession))
        {
            tasks.Add(sshSession.CloseAsync(SshDisconnectReason.ByApplication));
        }

        await Task.WhenAll(tasks);
        this.clientSessionTasks.Clear();
    }

    /// <inheritdoc />
    protected override async Task StartAsync(Tunnel tunnel, string[]? hostPublicKeys, CancellationToken cancellation)
    {
        string? accessToken = null;
        tunnel.AccessTokens?.TryGetValue(TunnelAccessScopes.Host, out accessToken);
        Requires.Argument(
            accessToken != null,
            nameof(tunnel),
            $"There is no access token for {nameof(TunnelAccessScopes.Host)} scope on the tunnel.");

        var endpoint = new TunnelRelayTunnelEndpoint
        {
            HostId = this.hostId,
            HostPublicKeys = hostPublicKeys,
        };

        endpoint = (TunnelRelayTunnelEndpoint)await ManagementClient.UpdateTunnelEndpointAsync(
            tunnel,
            endpoint,
            options: null,
            cancellation);

        Tunnel = tunnel;

        Requires.Argument(
            !string.IsNullOrEmpty(endpoint?.HostRelayUri),
            nameof(tunnel),
            $"The tunnel host relay endpoint URI is missing.");

        var hostRelayUri = endpoint.HostRelayUri;
        Trace.TraceInformation("Connecting to host tunnel relay {0}", hostRelayUri);

        try
        {
            var stream = await StreamFactory.CreateRelayStreamAsync(
                new Uri(hostRelayUri, UriKind.Absolute),
                accessToken,
                WebSocketSubProtocol,
                cancellation);

            this.hostSession = new MultiChannelStream(stream, Trace.WithName("HostSSH"));
            this.hostSession.ChannelOpening += HostSession_ChannelOpening;
            this.hostSession.Closed += HostSession_Closed;
            try
            {
                await this.hostSession.ConnectAsync(cancellation);
            }
            catch
            {
                await this.hostSession.CloseAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw TunnelConnectionException.FromInnerException(ex);
        }
    }

    private void HostSession_Closed(object? sender, SshSessionClosedEventArgs e)
    {
        var session = (MultiChannelStream)sender!;
        session.Closed -= HostSession_Closed;
        session.ChannelOpening -= HostSession_ChannelOpening;
        this.hostSession = null;
        Trace.TraceInformation("Connection to host tunnel relay closed.");
    }

    private void HostSession_ChannelOpening(object? sender, SshChannelOpeningEventArgs e)
    {
        if (!e.IsRemoteRequest)
        {
            // Auto approve all local requests (not that there are any for the time being).
            return;
        }

        if (e.Channel.ChannelType != ClientStreamChannelType)
        {
            e.FailureDescription = $"Unexpected channel type. Only {ClientStreamChannelType} is supported.";
            e.FailureReason = SshChannelOpenFailureReason.UnknownChannelType;
            return;
        }

        Task task;
        lock (this.clientSessionTasks)
        {
            if (this.disposeCts.IsCancellationRequested)
            {
                e.FailureDescription = $"The host is disconnecting.";
                e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                return;
            }

            task = AcceptClientSessionAsync((MultiChannelStream)sender!, this.disposeCts.Token);
            this.clientSessionTasks.Add(task);
        }

        task.ContinueWith(RemoveClientSessionTask);

        void RemoveClientSessionTask(Task t)
        {
            lock (this.clientSessionTasks)
            {
                this.clientSessionTasks.Remove(t);
            }
        }
    }

    private async Task AcceptClientSessionAsync(MultiChannelStream hostSession, CancellationToken cancellation)
    {
        try
        {
            using var stream = await hostSession.AcceptStreamAsync(ClientStreamChannelType, cancellation);
            await ConnectAndRunClientSessionAsync(stream, cancellation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceEvent(TraceEventType.Error, 0, "Error running client SSH session: {0}", exception.Message);
        }
    }

    private async Task ConnectAndRunClientSessionAsync(Stream stream, CancellationToken cancellation)
    {
        var serverConfig = new SshSessionConfiguration();

        // Enable port-forwarding via the SSH protocol.
        serverConfig.AddService(typeof(PortForwardingService));

        var session = new SshServerSession(serverConfig, Trace.WithName("ClientSSH"));
        session.Credentials = new SshServerCredentials(this.HostPrivateKey);

        var tcs = new TaskCompletionSource<object?>();
        using var tokenRegistration = cancellation.CanBeCanceled ?
            cancellation.Register(() => tcs.TrySetCanceled(cancellation)) : default;

        session.Authenticating += OnSshClientAuthenticating;
        session.ClientAuthenticated += OnSshClientAuthenticated;
        session.ChannelOpening += OnSshChannelOpening;
        session.Closed += Session_Closed;

        try
        {
            var portForwardingService = session.ActivateService<PortForwardingService>();

            // All tunnel hosts and clients should disable this because they do not use it (for now) and leaving it enabled is a potential security issue.
            portForwardingService.AcceptRemoteConnectionsForNonForwardedPorts = false;

            await session.ConnectAsync(stream, cancellation);
            this.SshSessions.Add(session);
            await tcs.Task;
        }
        finally
        {
            session.Authenticating -= OnSshClientAuthenticating;
            session.ChannelOpening -= OnSshChannelOpening;
            session.Closed -= Session_Closed;
        }

        void Session_Closed(object? sender, SshSessionClosedEventArgs e)
        {
            if (e.Reason == SshDisconnectReason.ByApplication)
            {
                Trace.TraceInformation("Client ssh session closed.");
            }
            else if (cancellation.IsCancellationRequested)
            {
                Trace.TraceInformation("Client ssh session cancelled.");
            }
            else
            {
                Trace.TraceEvent(
                    TraceEventType.Error,
                    0,
                    "Client ssh session closed unexpectely due to {0}, \"{1}\"\n{2}",
                    e.Reason,
                    e.Message,
                    e.Exception);
            }

            tcs.TrySetResult(null);
        }
    }

    private void OnSshClientAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        if (e.AuthenticationType == SshAuthenticationType.ClientNone)
        {
            // For now, the client is allowed to skip SSH authentication;
            // they must have a valid tunnel access token already to get this far.
            e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
        }
        else
        {
            // Other authentication types are not implemented. Doing nothing here
            // results in a client authentication failure.
        }
    }

    private async void OnSshClientAuthenticated(object? sender, EventArgs e)
    {
        // After the client session authenicated, automatically start forwarding existing ports.
        var session = (SshServerSession)sender!;
        var pfs = session.ActivateService<PortForwardingService>();

        foreach (TunnelPort port in Tunnel!.Ports ?? Enumerable.Empty<TunnelPort>())
        {
            try
            {
                await ForwardPortAsync(pfs, port, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Trace.TraceEvent(
                    TraceEventType.Error,
                    0,
                    "Error forwarding port {0} to client: {1}",
                    port.PortNumber,
                    exception.Message);
            }
        }
    }

    private void OnSshChannelOpening(object? sender, SshChannelOpeningEventArgs e)
    {
        if (e.Request is not PortForwardChannelOpenMessage portForwardRequest)
        {
            if (e.Request is ChannelOpenMessage channelOpenMessage)
            {
                // This allows the Go SDK to open an unused terminal channel
                if (channelOpenMessage.ChannelType == "session")
                {
                    return;
                }
            }

            Trace.Warning("Rejecting request to open non-portforwarding channel.");
            e.FailureReason = SshChannelOpenFailureReason.AdministrativelyProhibited;
            return;
        }

        if (portForwardRequest.ChannelType == "direct-tcpip")
        {
            if (!Tunnel!.Ports!.Any((p) => p.PortNumber == portForwardRequest.Port))
            {
                Trace.Warning("Rejecting request to connect to non-forwarded port:" +
                    portForwardRequest.Port);
                e.FailureReason = SshChannelOpenFailureReason.AdministrativelyProhibited;
            }
        }
        else if (portForwardRequest.ChannelType == "forwarded-tcpip")
        {
            if (sender is not SshServerSession sshSession)
            {
                Trace.Warning("Rejecting request due to invalid sender");
                e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                return;
            }
            else
            {
                var sessionId = sshSession.SessionId;
                if (sessionId == null)
                {
                    Trace.Warning("Rejecting request as session has no Id");
                    e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                    return;
                }

                if (!RemoteForwarders.ContainsKey(new SessionPortKey(sessionId, (ushort)portForwardRequest.Port)))
                {
                    Trace.Warning("Rejecting request to connect to non-forwarded port:" +
                        portForwardRequest.Port);
                    e.FailureReason = SshChannelOpenFailureReason.AdministrativelyProhibited;
                }
            }
        }
        else
        {
            Trace.Warning("Nonrecognized channel type " + portForwardRequest.ChannelType);
            e.FailureReason = SshChannelOpenFailureReason.UnknownChannelType;
        }
    }
}
