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
public class TunnelRelayTunnelHost : TunnelHost
{
    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v1 host protocol.
    /// </summary>
    public override string WebSocketSubProtocol => HostWebSocketSubProtocol;

    /// <summary>
    /// Web socket sub-protocol to connect to the tunnel relay endpoint with v2 host protocol.
    /// (The "-dev" suffix will be dropped when the v2 protocol is stable.)
    /// </summary>
    public override string WebSocketSubProtocolV2 => HostWebSocketSubProtocolV2;

    /// <summary>
    /// Ssh channel type in host relay ssh session where client session streams are passed.
    /// </summary>
    public const string ClientStreamChannelType = "client-ssh-session-stream";

    private readonly IList<Task> clientSessionTasks = new List<Task>();
    private readonly string hostId;
    private readonly ICollection<SshServerSession> reconnectableSessions = new List<SshServerSession>();

    private string endpointId { get { return hostId + "-relay"; } }

    /// <summary>
    /// Creates a new instance of a host that connects to a tunnel via a tunnel relay.
    /// </summary>
    public TunnelRelayTunnelHost(ITunnelManagementClient managementClient, TraceSource trace)
        : base(managementClient, trace)
    {
        this.hostId = MultiModeTunnelHost.HostId;
    }

    /// <inheritdoc/>
    protected override string ConnectionId => this.hostId;

    /// <summary>
    /// Get or set synthetic endpoint signature for the endpoint created for the host
    /// when connecting.
    /// <c>null</c> if the endpoint has not been created yet.
    /// </summary>
    private string? EndpointSignature { get; set; }

    /// <inheritdoc/>
    public override Task ConnectAsync(Tunnel tunnel, TunnelConnectionOptions? options, CancellationToken cancellation = default)
    {
        // If another host for the same tunnel connects, the first connection is disconnected
        // with "too many connections" reason. Reconnecting it again would cause the second host to
        // be kicked out, and then it would try to reconnect, kicking out this one.
        // To prevent this tug of war, do not allow reconnection in this case.
        if (DisconnectReason == SshDisconnectReason.TooManyConnections)
        {
            throw new TunnelConnectionException(
                "Cannot retry connection because another host for this tunnel has connected. " +
                "Only one host connection at a time is supported.");
        }

        return base.ConnectAsync(tunnel, options, cancellation);
    }

    /// <inheritdoc />
    protected override async Task DisposeConnectionAsync()
    {
        await base.DisposeConnectionAsync();

        List<Task> tasks;
        lock (DisposeLock)
        {
            tasks = new List<Task>(this.clientSessionTasks);
            this.clientSessionTasks.Clear();
        }

        // If the tunnel is present, the endpoint was created, and this host was not closed because of
        // too many connections, delete the endpoint.
        // Too many connections closure means another host has connected, and that other host, while
        // connecting, would have updated the endpoint. So this host won't be able to delete it anyway.
        if (Tunnel != null &&
            !string.IsNullOrEmpty(EndpointSignature) &&
            DisconnectReason != SshDisconnectReason.TooManyConnections)
        {
            tasks.Add(ManagementClient!.DeleteTunnelEndpointsAsync(Tunnel, endpointId));
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
        if (SshSession != null)
        {
            throw new InvalidOperationException(
                "Already connected. Use separate instances to connect to multiple tunnels.");
        }

        Requires.NotNull(Tunnel!, nameof(Tunnel));
        Requires.Argument(this.accessToken != null, nameof(Tunnel), $"There is no access token for {TunnelAccessScope} scope on the tunnel.");

        var hostPublicKey = HostPrivateKey.GetPublicKeyBytes(HostPrivateKey.KeyAlgorithmName).ToBase64();
        var tunnelHasSshPort = Tunnel.Ports != null &&
            Tunnel.Ports.Any((p) => p.Protocol == TunnelProtocol.Ssh);
        var endpointSignature =
            $"{Tunnel.TunnelId}.{Tunnel.ClusterId}:" +
            $"{Tunnel.Name}.{Tunnel.Domain}:" +
            $"{tunnelHasSshPort}:{this.hostId}:{hostPublicKey}";

        if (!string.Equals(endpointSignature, EndpointSignature, StringComparison.OrdinalIgnoreCase) ||
            RelayUri == null)
        {
            var endpoint = new TunnelRelayTunnelEndpoint
            {
                HostId = this.hostId,
                Id = this.endpointId,
                HostPublicKeys = new[] { hostPublicKey },
            };

            List<KeyValuePair<string, string>>? additionalQueryParams = null;
            if (tunnelHasSshPort)
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

            EndpointSignature = endpointSignature;
            Requires.Argument(
                !string.IsNullOrEmpty(endpoint?.HostRelayUri),
                nameof(Tunnel),
                $"The tunnel host relay endpoint URI is missing.");

            RelayUri = new Uri(endpoint.HostRelayUri, UriKind.Absolute);
        }

        return new RelayTunnelConnector(this);
    }

    /// <inheritdoc />
    protected override async Task ConfigureSessionAsync(Stream stream, bool isReconnect, TunnelConnectionOptions? options, CancellationToken cancellation)
    {
        SshClientSession session;
        if (ConnectionProtocol == WebSocketSubProtocol)
        {
            // The V1 protocol always configures no security, equivalent to SSH MultiChannelStream.
            // The websocket transport is still encrypted and authenticated.
            var sessionConfig = new SshSessionConfiguration(useSecurity: false) { KeepAliveTimeoutInSeconds = options?.KeepAliveIntervalInSeconds ?? 0 };
            session = new SshClientSession(sessionConfig, Trace.WithName("HostSSH"));
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
            if (options?.KeepAliveIntervalInSeconds > 0)
            {
                config.KeepAliveTimeoutInSeconds = options.KeepAliveIntervalInSeconds;
            }

            session = new SshClientSession(config, Trace.WithName("HostSSH"));

            var hostPfs = session.ActivateService<PortForwardingService>();
            hostPfs.MessageFactory = this;
        }

        session.KeepAliveFailed += (_, e) =>
        {
            OnKeepAliveFailed(e.Count);
        };
        session.KeepAliveSucceeded += (_, e) =>
        {
            OnKeepAliveSucceeded(e.Count);
        };

        SshSession = session;
        SubscribeSessionEvents(session);
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

    private void OnHostSessionChannelOpening(object? sender, SshChannelOpeningEventArgs e)
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
                this.reconnectableSessions,
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

    private async Task ConnectAndRunClientSessionAsync(SshStream stream, CancellationToken cancellation)
    {
        var channelId = stream.Channel.ChannelId;
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
            session.Closed += OnClientSessionClosed;

            try
            {
                var portForwardingService = session.ActivateService<PortForwardingService>();

                // All tunnel hosts and clients should disable this because they do not use it (for now) and leaving it enabled is a potential security issue.
                portForwardingService.AcceptRemoteConnectionsForNonForwardedPorts = false;

                await session.ConnectAsync(stream, cancellation);
                sshSessionOwnsStream = true;

                AddClientSshSession(session);

                if (Tunnel != null)
                {
                    var connectedEvent = new TunnelEvent($"host_client_connect");
                    connectedEvent.Properties = new Dictionary<string, string>
                    {
                        ["ClientChannelId"] = channelId.ToString(),
                        ["ClientSessionId"] = session.GetShortSessionId(),
                        ["HostSessionId"] = ConnectionId,
                    };
                    ManagementClient?.ReportEvent(Tunnel, connectedEvent);
                }

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
                session.Closed -= OnClientSessionClosed;

                RemoveClientSshSession(session);
            }
        }
        catch when (!sshSessionOwnsStream)
        {
            stream.Close();
            throw;
        }

        async void OnSshClientReconnected(object? sender, EventArgs e)
        {
            var session = (SshSession)sender!;
            if (Tunnel != null)
            {
                var reconnectedEvent = new TunnelEvent($"host_client_reconnect");
                reconnectedEvent.Properties = new Dictionary<string, string>
                {
                    ["ClientChannelId"] = channelId.ToString(),
                    ["ClientSessionId"] = session.GetShortSessionId(),
                    ["HostSessionId"] = ConnectionId,
                };
                ManagementClient?.ReportEvent(Tunnel, reconnectedEvent);
            }

            await StartForwardingExistingPortsAsync(
                (SshServerSession)sender!,
                removeUnusedPorts: true);
        }

        void OnClientSessionClosed(object? sender, SshSessionClosedEventArgs e)
        {
            var session = (SshSession)sender!;
            var trace = session.Trace;
            string? details = null;
            string? severity = null;

            // Reconnecting client session may cause the new session to close with 'None' reason.
            if (cancellation.IsCancellationRequested)
            {
                details = "Session cancelled.";
                trace.Verbose(details);
            }
            else if (e.Reason == SshDisconnectReason.ByApplication)
            {
                details = "Session closed.";
                trace.Verbose(details);
            }
            else if (e.Reason != SshDisconnectReason.None)
            {
                string messageFormat = "Session closed unexpectedly due to {0}, \"{1}\"\n{2}";
                details = string.Format(messageFormat, e.Reason, e.Message, e.Exception);
                severity = TunnelEvent.Error;
                trace.TraceEvent(
                    TraceEventType.Error,
                    0,
                    messageFormat,
                    e.Reason,
                    e.Message,
                    e.Exception);
            }

            if (Tunnel != null)
            {
                var disconnectedEvent = new TunnelEvent($"host_client_disconnect");
                disconnectedEvent.Severity = severity;
                disconnectedEvent.Details = details;
                disconnectedEvent.Properties = new Dictionary<string, string>
                {
                    ["ClientChannelId"] = channelId.ToString(),
                    ["ClientSessionId"] = session.GetShortSessionId(),
                    ["HostSessionId"] = ConnectionId,
                };
                ManagementClient?.ReportEvent(Tunnel, disconnectedEvent);
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

    private async Task StartForwardingExistingPortsAsync(
        SshSession session, bool removeUnusedPorts = false)
    {
        try
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
                var unusedLocalPorts = new HashSet<int>(
                    pfs.LocalForwardedPorts
                        .Select(p => p.LocalPort)
                        .Where(localPort => localPort.HasValue &&
                            !tunnelPorts.Any(tp => tp.PortNumber == localPort))
                        .Select(localPort => localPort!.Value));

                var remoteForwardersToDispose = RemoteForwarders
                    .Where((kvp) =>
                        ((kvp.Key.SessionId == null && session.SessionId == null) ||
                            ((kvp.Key.SessionId != null && session.SessionId != null) &&
                            Enumerable.SequenceEqual(kvp.Key.SessionId, session.SessionId))) &&
                        unusedLocalPorts.Contains(kvp.Value.LocalPort))
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
        catch (Exception ex)
        {
            // Catch unexpected exceptions because this method is called from async void methods.
            TraceSource trace = session.Trace;
            trace.TraceEvent(
                TraceEventType.Error,
                0,
                "Unhandled exception when forwarding ports.\n{0}",
                ex);
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
        this.OnReportProgress(TunnelProgress.StartingRefreshPorts);
        if (! await RefreshTunnelAsync(includePorts: true, cancellation))
        {
            return;
        }

        var updatedPorts = Tunnel?.Ports ?? Array.Empty<TunnelPort>();
        var forwardTasks = new List<Task>();

        var sessions = SshSessions.Cast<SshSession?>();
        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // In the V2 protocol, ports are forwarded direclty on the host session.
            // (But even when the host is V2, some clients may still connect with V1.)
            sessions = sessions.Append(SshSession);
        }

        foreach (var port in updatedPorts)
        {
            // For all sessions which are connected and authenticated, forward any added/updated
            // ports. For sessions that are not yet authenticated, the ports will be forwarded
            // immediately after authentication completes - see OnSshClientAuthenticated().
            // (Session requests may not be sent before the session is authenticated, for sessions
            // that require authentication; For V2 sessions that are not encrypted/authenticated
            // at all, the session ID is null.)
            //
            // If authentication completes concurrently, or if there are concurrent calls to this
            // method, then duplicate requests may be sent. Clients should ignore any duplicate
            // requests.
            foreach (var session in sessions.Where(
                (s) => s?.IsConnected == true && (s.SessionId == null || s.Principal != null)))
            {
                var key = new SessionPortKey(session!.SessionId, port.PortNumber);
                if (!RemoteForwarders.ContainsKey(key))
                {
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
        this.OnReportProgress(TunnelProgress.CompletedRefreshPorts);
    }

    /// <inheritdoc/>
    protected override void SubscribeSessionEvents(SshClientSession session)
    {
        base.SubscribeSessionEvents(session);
        session.ChannelOpening += OnHostSessionChannelOpening;
        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // Relay server authentication is done via the websocket TLS host certificate.
            // If SSH encryption/authentication is used anyway, just accept any SSH host key.
            session.Authenticating += static (_, e) =>
                e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());

            var hostPfs = session.GetService<PortForwardingService>()!
                ?? throw new InvalidOperationException($"{nameof(PortForwardingService)} is not activated.");

            hostPfs.ForwardedPortConnecting += OnForwardedPortConnecting;
        }
    }

    /// <inheritdoc/>
    protected override void UnsubscribeSessionEvents(SshClientSession session)
    {
        base.UnsubscribeSessionEvents(session);
        session.ChannelOpening -= OnHostSessionChannelOpening;
        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // session.Authenticating doesn't need unsubscribing because its event handler is static.

            var hostPfs = session.GetService<PortForwardingService>();
            if (hostPfs != null)
            {
                hostPfs.ForwardedPortConnecting -= OnForwardedPortConnecting;
            }
        }
    }
}
