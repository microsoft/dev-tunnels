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
using Microsoft.DevTunnels.Connections.Messages;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Ssh.Tcp.Events;

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
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public const string WebSocketSubProtocolV2 = "tunnel-relay-host-v2-dev";

    /// <summary>
    /// Ssh channel type in host relay ssh session where client session streams are passed.
    /// </summary>
    public const string ClientStreamChannelType = "client-ssh-session-stream";

    private readonly IList<Task> clientSessionTasks = new List<Task>();
    private readonly string hostId;
    private readonly ICollection<SshServerSession> reconnectableSessions = new List<SshServerSession>();

    private SshClientSession? hostSession;
    private Uri? relayUri;

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
        await base.DisposeAsync();

        var hostSession = this.hostSession;
        if (hostSession != null)
        {
            this.hostSession = null;
            await hostSession.CloseAsync(SshDisconnectReason.None);
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
        var protocols = Environment.GetEnvironmentVariable("DEVTUNNELS_PROTOCOL_VERSION") switch
        {
            "1" => new[] { WebSocketSubProtocol },
            "2" => new[] { WebSocketSubProtocolV2 },

            // By default, prefer V2 and fall back to V1.
            _ => new[] { WebSocketSubProtocolV2, WebSocketSubProtocol },
        };

        ValidateAccessToken();
        Trace.TraceInformation("Connecting to host tunnel relay {0}", this.relayUri!.AbsoluteUri);
        var (stream, subprotocol) = await this.StreamFactory.CreateRelayStreamAsync(
            this.relayUri!,
            this.accessToken,
            protocols,
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
            await hostSession.CloseAsync(disconnectReason);
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
        SshClientSession session;
        if (ConnectionProtocol == WebSocketSubProtocol)
        {
            // The V1 protocol always configures no security, equivalent to SSH MultiChannelStream.
            // The websocket transport is still encrypted and authenticated.
            session = new SshClientSession(
                SshSessionConfiguration.NoSecurity, Trace.WithName("HostSSH"));
        }
        else
        {
            // The V2 protocol configures optional encryption, including "none" as an enabled and
            // preferred key-exchange algorithm, because encryption of the outer SSH session is
            // optional since it is already over a TLS websocket.
            var config = new SshSessionConfiguration();
            config.KeyExchangeAlgorithms.Clear();
            config.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.None);
            config.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.EcdhNistp384);
            config.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.EcdhNistp256);
            config.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.DHGroup16Sha512);
            config.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.DHGroup14Sha256);

            config.AddService(typeof(PortForwardingService));
            session = new SshClientSession(config, Trace.WithName("HostSSH"));

            // Relay server authentication is done via the websocket TLS host certificate.
            // If SSH encryption/authentication is used anyway, just accept any SSH host key.
            session.Authenticating += (_, e) =>
                e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());

            var hostPfs = session.ActivateService<PortForwardingService>();
            hostPfs.MessageFactory = this;
            hostPfs.ForwardedPortConnecting += OnForwardedPortConnecting;
        }

        this.hostSession = session;
        session.ChannelOpening += HostSession_ChannelOpening;
        session.Closed += HostSession_Closed;
        await session.ConnectAsync(stream, cancellation);

        // SSH authentication is skipped in V1 protocol, optional in V2 depending on whether the
        // session performed a key exchange (as indicated by having a session ID or not). In the
        // latter case a password is not required. Strong authentication was already handled by
        // the relay service via the tunnel access token used for the websocket connection.
        if (session.SessionId != null)
        {
            var clientCredentials = new SshClientCredentials("tunnel", password: null);
            await session.AuthenticateAsync(clientCredentials);
        }

        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // In the v2 protocol, the host starts "forwarding" the ports as soon as it connects.
            // Then the relay will forward the forwarded ports to clients as they connect.
            await StartForwardingExistingPortsAsync(session);
        }
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
        var session = (SshClientSession)sender!;
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

        if (ConnectionProtocol == WebSocketSubProtocolV2 &&
            e.Channel.ChannelType == "forwarded-tcpip")
        {
            // With V2 protocol, the relay server always sends an extended channel open message
            // with a property indicating whether E2E encryption is requested for the connection.
            // The host returns an extended response message indicating if E2EE is enabled.
            var relayRequestMessage = e.Channel.OpenMessage
                .ConvertTo<PortRelayConnectRequestMessage>();
            var responseMessage = new PortRelayConnectResponseMessage();

            // The host can enable encryption for the channel if the client requested it.
            responseMessage.IsE2EEncryptionEnabled = this.EnableE2EEncryption &&
                relayRequestMessage.IsE2EEncryptionRequested;

            // In the future the relay might send additional information in the connect
            // request message, for example a user identifier that would enable the host to
            // group channels by user.

            e.OpeningTask = Task.FromResult<ChannelMessage>(responseMessage);
            return;
        }
        else if (e.Channel.ChannelType != ClientStreamChannelType)
        {
            e.FailureDescription = $"Unknown channel type: {e.Channel.ChannelType}.";
            e.FailureReason = SshChannelOpenFailureReason.UnknownChannelType;
            return;
        }

        // V1 protocol.

        // Increase max window size to work around channel congestion bug.
        // This does not entirely eliminate the problem, but reduces the chance.
        e.Channel.MaxWindowSize = SshChannel.DefaultMaxWindowSize * 5;

        Task task;
        lock (DisposeLock)
        {
            if (DisposeToken.IsCancellationRequested)
            {
                e.FailureDescription = $"The host is disconnecting.";
                e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                return;
            }

            task = AcceptClientSessionAsync(e.Channel, DisposeToken);
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

    /// <summary>
    /// Encrypts the channel if necessary when a port connection is established (v2 only).
    /// </summary>
    protected override void OnForwardedPortConnecting(
        object? sender, ForwardedPortConnectingEventArgs e)
    {
        var channel = e.Stream.Channel;
        var relayRequestMessage = channel.OpenMessage
            .ConvertTo<PortRelayConnectRequestMessage>();

        bool isE2EEncryptionEnabled = this.EnableE2EEncryption &&
            relayRequestMessage.IsE2EEncryptionRequested;
        if (isE2EEncryptionEnabled)
        {
            // Increase the max window size so that it is at least larger than the window
            // size of one client channel.
            channel.MaxWindowSize = SshChannel.DefaultMaxWindowSize * 2;

            SshServerCredentials serverCredentials =
                new SshServerCredentials(HostPrivateKey);
            var secureStream = new SecureStream(
                e.Stream,
                serverCredentials,
                channel.Trace.WithName(channel.Trace.Name + "." + channel.ChannelId));

            // The client was already authenticated by the relay.
            secureStream.Authenticating += (_, e) =>
                e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(
                    new ClaimsPrincipal());

            e.TransformTask = Task.FromResult<Stream?>(secureStream);

            // The client will connect to the secure stream after the channel is opened.
            ConnectEncryptedChannel();
            async void ConnectEncryptedChannel()
            {
                try
                {
                    // Do not pass the cancellation token from the connecting event,
                    // because the connection will outlive the event.
                    await secureStream.ConnectAsync();
                }
                catch (Exception ex)
                {
                    // Catch all exceptions in this async void method.
                    try
                    {
                        channel.Trace.Error("Error connecting encrypted channel: " + ex.Message);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        base.OnForwardedPortConnecting(sender, e);
    }

    private async Task AcceptClientSessionAsync(SshChannel clientSessionChannel, CancellationToken cancellation)
    {
        try
        {
            var stream = new SshStream(clientSessionChannel);
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
        SshSession session, bool removeUnusedPorts = false)
    {
        // Send port-forward request messages concurrently. The client may still handle the
        // requests sequentially but at least there is no network round-trip between them.
        var forwardTasks = new List<Task>();

        var tunnelPorts = Tunnel!.Ports ?? Enumerable.Empty<TunnelPort>();
        var pfs = session.ActivateService<PortForwardingService>();
        pfs.ForwardConnectionsToLocalPorts = this.ForwardConnectionsToLocalPorts;
        foreach (TunnelPort port in tunnelPorts)
        {
            // ForwardPortAsync() catches and logs most exceptions that might normally occur.
            forwardTasks.Add(ForwardPortAsync(pfs, port, CancellationToken.None));
        }

        await Task.WhenAll(forwardTasks);

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
                    ((kvp.Key.SessionId == null && session.SessionId == null) ||
                        ((kvp.Key.SessionId != null && session.SessionId != null) &&
                        Enumerable.SequenceEqual(kvp.Key.SessionId, session.SessionId))) &&
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
            var eventArgs = new ForwardedPortConnectingEventArgs(
                (int)portForwardRequest.Port, false, new SshStream(e.Channel), CancellationToken.None);
            base.OnForwardedPortConnecting(this, eventArgs);
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

    /// <inheritdoc />
    public override async Task RefreshPortsAsync(CancellationToken cancellation)
    {
        if (Tunnel == null || ManagementClient == null)
        {
            return;
        }

        var updatedTunnel = await ManagementClient.GetTunnelAsync(
            Tunnel, new TunnelRequestOptions { IncludePorts = true });

        var updatedPorts = updatedTunnel?.Ports ?? Array.Empty<TunnelPort>();
        Tunnel.Ports = updatedPorts;

        var forwardTasks = new List<Task>();

        var sessions = SshSessions.Cast<SshSession?>();
        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // In the V2 protocol, ports are forwarded direclty on the host session.
            // (But even when the host is V2, some clients may still connect with V1.)
            sessions = sessions.Append(this.hostSession);
        }

        foreach (var port in updatedPorts)
        {
            foreach (var session in sessions.Where((s) => s?.IsConnected == true))
            {
                var key = new SessionPortKey(session!.SessionId!, port.PortNumber);
                if (!RemoteForwarders.ContainsKey(key))
                {
                    // Overlapping refresh operations could cause duplicate forward requests to be
                    // sent to clients, but clients should ignore the duplicate requests.
                    var pfs = session.GetService<PortForwardingService>()!;
                    forwardTasks.Add(ForwardPortAsync(pfs, port, cancellation));
                }
            }
        }

        foreach (var forwarder in RemoteForwarders)
        {
            if (!updatedPorts.Any((p) => p.PortNumber == forwarder.Value.RemotePort))
            {
                // Since RemoteForwarders is a concurrent dictionary, overlapping refresh
                // operations will only be able to remove and dispose a forwarder once.
                if (RemoteForwarders.TryRemove(forwarder.Key, out _))
                {
                    forwarder.Value.Dispose();
                }
            }
        }

        await Task.WhenAll(forwardTasks);
    }
}
