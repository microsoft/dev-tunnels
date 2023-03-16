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
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Tunnel host implementation that uses data-plane relay
/// to accept client connections.
/// </summary>
public class TunnelRelayTunnelHost : TunnelHost, IRelayClient
{
    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 host protocol.
    /// </summary>
    public const string WebSocketSubProtocol = "tunnel-relay-host";

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 host protocol.
    /// </summary>
    public const string WebSocketSubProtocolV2 = "tunnel-relay-host-v2";

    /// <summary>
    /// Ssh channel type in host relay ssh session where client session streams are passed.
    /// </summary>
    public const string ClientStreamChannelType = "client-ssh-session-stream";

    private readonly IList<Task> clientSessionTasks = new List<Task>();
    private readonly string hostId;
    private readonly ICollection<SshServerSession> reconnectableSessions = new List<SshServerSession>();

    private MultiChannelStream? hostSession;
    private Uri? relayUri;

    /// <summary>
    /// Creates a new instance of a host that connects to a tunnel via a tunnel relay.
    /// </summary>
    public TunnelRelayTunnelHost(ITunnelManagementClient managementClient, TraceSource trace)
        : base(managementClient, trace)
    {
        this.hostId = MultiModeTunnelHost.HostId;
    }

    /// <inheritdoc/>
    public string? ConnectionProtocol { get; private set; }

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
        await base.DisposeAsync();

        var hostSession = this.hostSession;
        if (hostSession != null)
        {
            this.hostSession = null;
            await hostSession.CloseAsync();
            hostSession.Dispose();
        }

        List<Task> tasks;
        lock (DisposeLock)
        {
            tasks = new List<Task>(this.clientSessionTasks);
            this.clientSessionTasks.Clear();
        }

        if (Tunnel != null)
        {
            tasks.Add(ManagementClient!.DeleteTunnelEndpointsAsync(Tunnel, this.hostId, TunnelConnectionMode.TunnelRelay));
        }

        foreach (RemotePortForwarder forwarder in RemoteForwarders.Values)
        {
            forwarder.Dispose();
        }

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    protected override async Task<ITunnelConnector> CreateTunnelConnectorAsync(CancellationToken cancellation)
    {
        Requires.NotNull(Tunnel!, nameof(Tunnel));

        this.accessToken = null!;
        Tunnel.AccessTokens?.TryGetValue(TunnelAccessScope, out this.accessToken!);
        Requires.Argument(this.accessToken != null, nameof(Tunnel), $"There is no access token for {TunnelAccessScope} scope on the tunnel.");

        var hostPublicKeys = new[]
        {
            HostPrivateKey.GetPublicKeyBytes(HostPrivateKey.KeyAlgorithmName).ToBase64(),
        };

        var endpoint = new TunnelRelayTunnelEndpoint
        {
            HostId = this.hostId,
            HostPublicKeys = hostPublicKeys,
        };
        List<KeyValuePair<string, string>>? additionalQueryParams = null;
        if (Tunnel.Ports != null && Tunnel.Ports.Any((p) => p.Protocol == TunnelProtocol.Ssh))
        {
            additionalQueryParams = new () {new KeyValuePair<string, string>("includeSshGatewayPublicKey", "true")};
        }

        endpoint = (TunnelRelayTunnelEndpoint)await ManagementClient!.UpdateTunnelEndpointAsync(
            Tunnel,
            endpoint,
            options: new TunnelRequestOptions()
            {
                AdditionalQueryParameters = additionalQueryParams,
            },
            cancellation);

        Requires.Argument(
            !string.IsNullOrEmpty(endpoint?.HostRelayUri),
            nameof(Tunnel),
            $"The tunnel host relay endpoint URI is missing.");

        this.relayUri = new Uri(endpoint.HostRelayUri, UriKind.Absolute);

        return new RelayTunnelConnector(this);
    }

    /// <summary>
    /// Create stream to the tunnel.
    /// </summary>
    protected virtual async Task<Stream> CreateSessionStreamAsync(CancellationToken cancellation)
    {
        ValidateAccessToken();
        Trace.TraceInformation("Connecting to host tunnel relay {0}", this.relayUri!.AbsoluteUri);
        var (stream, subprotocol) = await this.StreamFactory.CreateRelayStreamAsync(
            this.relayUri!,
            this.accessToken,
            new[] { WebSocketSubProtocol },
            cancellation);
        Trace.TraceEvent(TraceEventType.Verbose, 0, "Connected with subprotocol '{0}'", subprotocol);
        ConnectionProtocol = subprotocol;
        return stream;
    }

    /// <inheritdoc />
    protected override async Task CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception)
    {
        await base.CloseSessionAsync(disconnectReason, exception);
        var hostSession = this.hostSession;
        if (hostSession != null)
        {
            await hostSession.CloseAsync();
            hostSession.Dispose();
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
    async Task IRelayClient.ConfigureSessionAsync(Stream stream, bool isReconnect, CancellationToken cancellation)
    {
        this.hostSession = new MultiChannelStream(stream, Trace.WithName("HostSSH"));

        // Increase max window size to work around channel congestion bug.
        // This does not entirely eliminate the problem, but reduces the chance.
        // TODO: Change the protocol to avoid layering SSH sessions, which will resolve the issue.
        this.hostSession.ChannelMaxWindowSize = SshChannel.DefaultMaxWindowSize * 5;

        this.hostSession.ChannelOpening += HostSession_ChannelOpening;
        this.hostSession.Closed += HostSession_Closed;
        await this.hostSession.ConnectAsync(cancellation);
    }

    /// <inheritdoc />
    Task IRelayClient.CloseSessionAsync(SshDisconnectReason disconnectReason, Exception? exception) =>
        CloseSessionAsync(disconnectReason, exception);

    /// <inheritdoc />
    Task<bool> IRelayClient.RefreshTunnelAccessTokenAsync(CancellationToken cancellation) =>
        RefreshTunnelAccessTokenAsync(cancellation);

    /// <inheritdoc />
    void IRelayClient.OnRetrying(RetryingTunnelConnectionEventArgs e) => OnRetrying(e);

    #endregion IRelayClient

    private void HostSession_Closed(object? sender, SshSessionClosedEventArgs e)
    {
        var session = (MultiChannelStream)sender!;
        session.Closed -= HostSession_Closed;
        session.ChannelOpening -= HostSession_ChannelOpening;
        this.hostSession = null;
        Trace.TraceInformation(
            "Connection to host tunnel relay closed.{0}",
            DisposeToken.IsCancellationRequested ? string.Empty : " Reconnecting.");

        if (e.Reason == SshDisconnectReason.ConnectionLost)
        {
            StartReconnectTaskIfNotDisposed();
        }
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
        lock (DisposeLock)
        {
            if (DisposeToken.IsCancellationRequested)
            {
                e.FailureDescription = $"The host is disconnecting.";
                e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                return;
            }

            task = AcceptClientSessionAsync((MultiChannelStream)sender!, DisposeToken);
            this.clientSessionTasks.Add(task);
        }

        task.ContinueWith(RemoveClientSessionTask);

        void RemoveClientSessionTask(Task t)
        {
            lock (DisposeLock)
            {
                this.clientSessionTasks.Remove(t);
            }
        }
    }

    private async Task AcceptClientSessionAsync(MultiChannelStream hostSession, CancellationToken cancellation)
    {
        try
        {
            var stream = await hostSession.AcceptStreamAsync(ClientStreamChannelType, cancellation);
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
        var sshSessionOwnsStream = false;
        var tcs = new TaskCompletionSource<object?>();
        try
        {
            // Always enable reconnect on client SSH server.
            // When a client reconnects, relay service just opens another SSH channel of client-ssh-session-stream type for it.
            var serverConfig = new SshSessionConfiguration(enableReconnect: true);

            // Enable port-forwarding via the SSH protocol.
            serverConfig.AddService(typeof(PortForwardingService));

            var session = new SshServerSession(serverConfig, this.reconnectableSessions, Trace.WithName("ClientSSH"));
            session.Credentials = new SshServerCredentials(this.HostPrivateKey);

            using var tokenRegistration = cancellation.CanBeCanceled ?
                cancellation.Register(() => tcs.TrySetCanceled(cancellation)) : default;

            session.Authenticating += OnSshClientAuthenticating;
            session.ClientAuthenticated += OnSshClientAuthenticated;
            session.Reconnected += OnSshClientReconnected;
            session.Request += OnClientSessionRequest;
            session.ChannelOpening += OnSshChannelOpening;
            session.Closed += Session_Closed;

            try
            {
                var portForwardingService = session.ActivateService<PortForwardingService>();

                // All tunnel hosts and clients should disable this because they do not use it (for now) and leaving it enabled is a potential security issue.
                portForwardingService.AcceptRemoteConnectionsForNonForwardedPorts = false;

                await session.ConnectAsync(stream, cancellation);
                sshSessionOwnsStream = true;

                AddClientSshSession(session);

                await tcs.Task;
            }
            finally
            {
                if (!session.IsClosed)
                {
                    await session.CloseAsync(SshDisconnectReason.ByApplication);
                }

                session.Authenticating -= OnSshClientAuthenticating;
                session.ClientAuthenticated -= OnSshClientAuthenticated;
                session.Reconnected -= OnSshClientReconnected;
                session.ChannelOpening -= OnSshChannelOpening;
                session.Closed -= Session_Closed;

                RemoveClientSshSession(session);
            }
        }
        catch when (!sshSessionOwnsStream)
        {
            stream.Close();
            throw;
        }

        void Session_Closed(object? sender, SshSessionClosedEventArgs e)
        {
            // Reconnecting client session may cause the new session close with 'None' reason and null exception.
            if (cancellation.IsCancellationRequested)
            {
                Trace.TraceInformation("Client ssh session cancelled.");
            }
            else if (e.Reason == SshDisconnectReason.ByApplication)
            {
                Trace.TraceInformation("Client ssh session closed.");
            }
            else if (e.Reason != SshDisconnectReason.None || e.Exception != null)
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

    private void OnClientSessionRequest(
        object? sender,
        SshRequestEventArgs<SessionRequestMessage> e)
    {
        if (e.RequestType == RefreshPortsRequestType)
        {
            e.ResponseTask = Task.Run<SshMessage>(async () =>
            {
                // This may send tcpip-forward or cancel-tcpip-forward requests to clients.
                // Forward requests (but not cancellations) wait for client responses.
                await RefreshPortsAsync(e.Cancellation);
                return new SessionRequestSuccessMessage();
            });
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

    private async void OnSshClientAuthenticated(object? sender, EventArgs e) =>
        await StartForwardingExistingPortsAsync((SshServerSession)sender!);

    private async void OnSshClientReconnected(object? sender, EventArgs e) =>
        await StartForwardingExistingPortsAsync((SshServerSession)sender!, removeUnusedPorts: true);

    private async Task StartForwardingExistingPortsAsync(
        SshServerSession session, bool removeUnusedPorts = false)
    {
        var tunnelPorts = Tunnel!.Ports ?? Enumerable.Empty<TunnelPort>();
        var pfs = session.ActivateService<PortForwardingService>();
        pfs.ForwardConnectionsToLocalPorts = this.ForwardConnectionsToLocalPorts;
        foreach (TunnelPort port in tunnelPorts)
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

        // If a tunnel client reconnects, its SSH session Port Forwarding service may
        // have remote port forwarders for the ports no longer forwarded.
        // Remove such forwarders.
        if (removeUnusedPorts && session.SessionId != null)
        {
            tunnelPorts = Tunnel!.Ports ?? Enumerable.Empty<TunnelPort>();
            var unusedlocalPorts = new HashSet<int>(
                pfs.LocalForwardedPorts
                    .Select(p => p.LocalPort)
                    .Where(localPort => localPort.HasValue && !tunnelPorts.Any(tp => tp.PortNumber == localPort))
                    .Select(localPort => localPort!.Value));

            var remoteForwardersToDispose = RemoteForwarders
                .Where((kvp) =>
                    Enumerable.SequenceEqual(kvp.Key.SessionId, session.SessionId) &&
                    unusedlocalPorts.Contains(kvp.Value.LocalPort))
                .Select(kvp => kvp.Key);

            foreach (SessionPortKey key in remoteForwardersToDispose)
            {
                if (RemoteForwarders.TryRemove(key, out var remoteForwarder))
                {
                    remoteForwarder?.Dispose();
                }
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
                if (channelOpenMessage.ChannelType == SshChannel.SessionChannelType)
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
            this.OnForwardedPortConnecting(portForwardRequest.Port, e.Channel);
        }
        // For forwarded-tcpip do not check RemoteForwarders because they may not be updated yet.
        // There is a small time interval in ForwardPortAsync() between the port
        // being forwarded with ForwardFromRemotePortAsync() and RemoteForwarders updated.
        // Setting PFS.AcceptRemoteConnectionsForNonForwardedPorts to false makes PFS reject forwarding requests from the
        // clients for the ports that are not forwarded and are missing in PFS.remoteConnectors.
        // Call to PFS.ForwardFromRemotePortAsync() in ForwardPortAsync() adds the connector to PFS.remoteConnectors.
        else
        {
            Trace.Warning("Unrecognized channel type " + portForwardRequest.ChannelType);
            e.FailureReason = SshChannelOpenFailureReason.UnknownChannelType;
        }
    }
}
