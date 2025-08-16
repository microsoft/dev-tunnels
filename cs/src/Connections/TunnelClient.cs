// <copyright file="TunnelClientBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
/// Base class for clients that connect to a single host
/// </summary>
public abstract class TunnelClient : TunnelRelayConnection, ITunnelClient
{
    private bool acceptLocalConnectionsForForwardedPorts = true;
    private IPAddress localForwardingHostAddress = IPAddress.Loopback;
    private readonly Dictionary<int, List<SecureStream>> disconnectedStreams = new();

    /// <summary>
    /// Creates a new instance of the <see cref="TunnelClient" /> class.
    /// </summary>
    public TunnelClient(ITunnelManagementClient? managementClient, TraceSource trace) : base(managementClient, trace)
    {
    }

    /// <inheritdoc />
    public abstract IReadOnlyCollection<TunnelConnectionMode> ConnectionModes { get; }

    /// <inheritdoc />
    public ForwardedPortsCollection? ForwardedPorts =>
        SshPortForwardingService?.RemoteForwardedPorts;

    /// <summary>
    /// One or more SSH public keys published by the host with the tunnel endpoint.
    /// </summary>
    protected string[]? HostPublicKeys { get; set; }

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

    /// <inheritdoc />
    public event EventHandler<PortForwardingEventArgs>? PortForwarding;

    /// <inheritdoc />
    public event EventHandler<ForwardedPortConnectingEventArgs>? ForwardedPortConnecting;

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
    /// Gets or sets a value indicating whether local connections for forwarded ports are
    /// accepted.
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
    /// Gets or sets the local network interface address that the tunnel client listens on when
    /// accepting connections for forwarded ports.
    /// </summary>
    /// <remarks>
    /// The default value is the loopback address (127.0.0.1). Applications may set this to the
    /// address indicating any interface (0.0.0.0) or to the address of a specific interface.
    /// The tunnel client supports both IPv4 and IPv6 when listening on either loopback or
    /// any interface.
    /// </remarks>
    public IPAddress LocalForwardingHostAddress
    {
        get => this.localForwardingHostAddress;
        set
        {
            if (value != this.localForwardingHostAddress)
            {
                this.localForwardingHostAddress = value;
                ConfigurePortForwardingService();
            }
        }
    }

    /// <summary>
    /// Get host Id the client is connecting to.
    /// </summary>
    public string? HostId { get; private set; }

    /// <inheritdoc />
    public override async Task ConnectAsync(
        Tunnel tunnel,
        TunnelConnectionOptions? options,
        CancellationToken cancellation = default)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        Requires.NotNull(tunnel.Endpoints!, nameof(Tunnel.Endpoints));

        if (this.SshSession?.IsConnected == true)
        {
            throw new InvalidOperationException(
                "Already connected. Use separate instances to connect to multiple tunnels.");
        }

        if (tunnel.Endpoints.Length == 0)
        {
            throw new InvalidOperationException(
                "No hosts are currently accepting connections for the tunnel.");
        }

        HostId = options?.HostId;
        await ConnectTunnelSessionAsync(tunnel, options, cancellation);
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
    /// SSH session reconnect is enabled only if <see cref="TunnelConnection.connector"/> is not null.
    /// </remarks>
    protected async Task StartSshSessionAsync(Stream stream, TunnelConnectionOptions? options, CancellationToken cancellation)
    {
        var session = this.SshSession;
        if (session != null)
        {
            // Unsubscribe event handler from the previous session.
            UnsubscribeSessionEvents(session);
        }

        // Enable V1 reconnect only if connector is set as reconnect depends on it.
        // (V2 SSH reconnect is handled by the SecureStream class.)
        var clientConfig = new SshSessionConfiguration(
            enableReconnect: this.connector != null &&
                ConnectionProtocol == WebSocketSubProtocol);

        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            // Configure optional encryption, including "none" as an enabled and preferred kex algorithm,
            // because encryption of the outer SSH session is optional since it is already over a TLS websocket.
            clientConfig.KeyExchangeAlgorithms.Clear();
            clientConfig.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.None);
            clientConfig.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.EcdhNistp384);
            clientConfig.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.EcdhNistp256);
            clientConfig.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.DHGroup16Sha512);
            clientConfig.KeyExchangeAlgorithms.Add(SshAlgorithms.KeyExchange.DHGroup14Sha256);
        }

        if (options?.KeepAliveIntervalInSeconds > 0)
        {
            clientConfig.KeepAliveTimeoutInSeconds = options.KeepAliveIntervalInSeconds;
        }

        // Enable port-forwarding via the SSH protocol.
        clientConfig.AddService(typeof(PortForwardingService));

        session = new SshClientSession(clientConfig, Trace.WithName("SSH"));
        SshSession = session;
        session.KeepAliveFailed += (_, e) =>
        {
            OnKeepAliveFailed(e.Count);
        };
        session.KeepAliveSucceeded += (_, e) =>
        {
            OnKeepAliveSucceeded(e.Count);
        };
        SshPortForwardingService = session.ActivateService<PortForwardingService>();
        ConfigurePortForwardingService();
        SubscribeSessionEvents(session);

        IsSshSessionActive = true;
        await session.ConnectAsync(stream, cancellation);

        // SSH authentication is required in V1 protocol, optional in V2 depending on whether the
        // session enabled key exchange (as indicated by having a session ID or not). In either case
        // a password is not required. Strong authentication was already handled by the relay
        // service via the tunnel access token used for the websocket connection.
        if (session.SessionId != null)
        {
            var clientCredentials = new SshClientCredentials("tunnel", password: null);
            if (!await session.AuthenticateAsync(clientCredentials))
            {
                // Server authentication happens first, and if it succeeds then it sets a principal.
                throw new SshConnectionException(
                    session.Principal == null ?
                    "SSH server authentication failed." : "SSH client authentication failed.");
            }
        }
    }

    private void OnSshSessionDisconnected(object? sender, EventArgs e) =>
        MaybeStartReconnecting(
            (SshSession)sender!,
            SshDisconnectReason.ConnectionLost,
            exception: new SshConnectionException("Connection lost.", SshDisconnectReason.ConnectionLost));

    private void ConfigurePortForwardingService()
    {
        var pfs = SshPortForwardingService;
        if (pfs == null)
        {
            if (ConnectionProtocol == WebSocketSubProtocolV2)
            {
                throw new InvalidOperationException($"{nameof(PortForwardingService)} is not activated.");
            }

            return;
        }

        // All tunnel hosts and clients should disable this because they do not use it (for now)
        // and leaving it enabled is a potential security issue.
        pfs.AcceptRemoteConnectionsForNonForwardedPorts = false;

        pfs.AcceptLocalConnectionsForForwardedPorts = this.acceptLocalConnectionsForForwardedPorts;
        if (pfs.AcceptLocalConnectionsForForwardedPorts)
        {
            pfs.TcpListenerFactory = new RetryTcpListenerFactory(this.localForwardingHostAddress);
        }

        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            pfs.MessageFactory = this;
        }
    }

    private void OnForwardedPortAdded(PortForwardingService pfs, ForwardedPortEventArgs e)
    {
        var port = e.Port.RemotePort;
        if (!port.HasValue)
        {
            return;
        }

        List<SecureStream>? streams;
        lock (this.disconnectedStreams)
        {
            // If there are disconnected streams for the port, re-connect them now.
            if (!this.disconnectedStreams.TryGetValue(port.Value, out streams))
            {
                streams = null;
            }
        }

        if (streams?.Count > 0)
        {
            this.Trace.Verbose(
                $"Reconnecting {streams.Count} stream(s) to forwarded port {port}");

            for (int i = streams.Count; i > 0; i--)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await pfs.ConnectToForwardedPortAsync(port.Value, CancellationToken.None);
                        this.Trace.Verbose($"Reconnected stream to forwarded port {port}");
                    }
                    catch (Exception ex)
                    {
                        this.Trace.Warning(
                            $"Failed to reconnect to forwarded port {port}: {ex.Message}");
                        lock (this.disconnectedStreams)
                        {
                            // The host is no longer accepting connections on the forwarded port?
                            // Dispose and clear the list of disconnected streams for the port,
                            // because it seems it is no longer possible to reconnect them.
                            while (streams.Count > 0)
                            {
                                streams[0].Dispose();
                                streams.RemoveAt(0);
                            }
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// Invoked when a forwarded port is connecting. (Only for V2 protocol.)
    /// </summary>
    protected virtual void OnForwardedPortConnecting(
        object? sender, ForwardedPortConnectingEventArgs e)
    {
        // With V2 protocol, the relay server always sends an extended response message
        // with a property indicating whether E2E encryption is enabled for the connection.
        var channel = e.Stream.Channel;
        var relayResponseMessage = channel.OpenConfirmationMessage
            .ConvertTo<PortRelayConnectResponseMessage>();

        if (relayResponseMessage.IsE2EEncryptionEnabled)
        {
            // The host trusts the relay to authenticate the client, so it doesn't require
            // any additional password/token for client authentication.
            var clientCredentials = new SshClientCredentials("tunnel");

            e.TransformTask = EncryptChannelAsync(e.Stream);
            async Task<Stream?> EncryptChannelAsync(SshStream channelStream)
            {
                SecureStream? secureStream = null;

                // If there's a disconnected SecureStream for the port, try to reconnect it.
                // If there are multiple, pick one and the host will match by SSH session ID.
                lock (this.disconnectedStreams)
                {
                    if (this.disconnectedStreams.TryGetValue(e.Port, out var streamsList) &&
                        streamsList.Count > 0)
                    {
                        secureStream = streamsList[0];
                        streamsList.RemoveAt(0);
                    }
                }

                var trace = channel.Trace.WithName(channel.Trace.Name + "." + channel.ChannelId);
                if (secureStream != null)
                {
                    trace.Verbose($"Reconnecting encrypted stream for port {e.Port}...");
                    await secureStream.ReconnectAsync(channelStream);
                    trace.Verbose($"Reconnecting encrypted stream for port {e.Port} succeeded.");
                }
                else
                {
                    secureStream = new SecureStream(
                        e.Stream, clientCredentials, enableReconnect: true, trace);
                    secureStream.Authenticating += OnHostAuthenticating;
                    secureStream.Disconnected += (_, _) => OnSecureStreamDisconnected(
                        e.Port, secureStream, trace);

                    // Do not pass the cancellation token from the connecting event,
                    // because the connection will outlive the event.
                    await secureStream.ConnectAsync();
                }

                return secureStream;
            }
        }

        this.ForwardedPortConnecting?.Invoke(this, e);
    }

    private void OnSecureStreamDisconnected(int port, SecureStream secureStream, TraceSource trace)
    {
        trace.Verbose($"Encrypted stream for port {port} disconnected.");

        lock (this.disconnectedStreams)
        {
            if (this.disconnectedStreams.TryGetValue(port, out var streamsList))
            {
                streamsList.Add(secureStream);
            }
            else
            {
                this.disconnectedStreams.Add(port, new List<SecureStream> { secureStream });
            }
        }
    }

    private void OnHostAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        // If this method returns without assigning e.AuthenticationTask, the auth fails.

        if (e.AuthenticationType != SshAuthenticationType.ServerPublicKey || e.PublicKey == null)
        {
            this.Trace.Warning("Invalid host authenticating event.");
            return;
        }

        // The public key property on this event comes from SSH key-exchange; at this point the
        // SSH server has cryptographically proven that it holds the corresponding private key.
        // Convert host key bytes to base64 to match the format in which the keys are published.
        var hostKey = e.PublicKey.GetPublicKeyBytes(e.PublicKey.KeyAlgorithmName).ToBase64();

        // Host public keys are obtained from the tunnel endpoint record published by the host.
        if (this.HostPublicKeys == null)
        {
            this.Trace.Warning(
                "Host identity could not be verified because no public keys were provided.");
            this.Trace.Verbose("Host key: " + hostKey);
            e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
        }
        else if (this.HostPublicKeys.Contains(hostKey))
        {
            this.Trace.Verbose("Verified host identity with public key " + hostKey);
            e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
        }
        else if (Tunnel != null && ManagementClient != null)
        {
            this.Trace.Verbose("Host public key verification failed. Refreshing tunnel.");
            this.Trace.Verbose("Host key: " + hostKey);
            this.Trace.Verbose("Expected key(s): " + string.Join(", ", this.HostPublicKeys));
            e.AuthenticationTask = RefreshTunnelAndAuthenticateHostAsync(hostKey, DisposeToken);
        }
        else
        {
            this.Trace.Error("Host public key verification failed.");
            this.Trace.Verbose("Host key: " + hostKey);
            this.Trace.Verbose("Expected key(s): " + string.Join(", ", this.HostPublicKeys));
        }
    }

    private async Task<ClaimsPrincipal?> RefreshTunnelAndAuthenticateHostAsync(string hostKey, CancellationToken cancellation)
    {
        try
        {
            await RefreshTunnelAsync(includePorts: false, cancellation);
        }
        catch (Exception exception)
        when (DisposeToken.IsCancellationRequested &&
            (exception is ObjectDisposedException || exception is OperationCanceledException))
        {
            // Client has been disposed while refreshing the tunnel.
            // The SSH session has closed or will close momentarily.
            this.Trace.Warning("Host public key verification failed. Tunnel client is disconnected.");
            return null;
        }

        if (Tunnel == null)
        {
            this.Trace.Warning("Host public key verification failed. Tunnel is not found.");
            return null;
        }

        if (this.HostPublicKeys == null)
        {
            this.Trace.Warning(
                "Host identity could not be verified because no public keys were provided.");

            return new ClaimsPrincipal();
        }

        if (this.HostPublicKeys.Contains(hostKey))
        {
            this.Trace.Verbose("Verified host identity with public key " + hostKey);
            return new ClaimsPrincipal();
        }

        this.Trace.Error("Host public key verification failed.");
        this.Trace.Verbose("Host key: " + hostKey);
        this.Trace.Verbose("Expected key(s): " + string.Join(", ", this.HostPublicKeys));
        return null;
    }

    private void OnSshServerAuthenticating(object? sender, SshAuthenticatingEventArgs e)
    {
        if (this.ConnectionProtocol == WebSocketSubProtocol)
        {
            // For V1 protocol the SSH server is the host; it should be authenticated with public key.
            OnHostAuthenticating(sender, e);
        }
        else
        {
            // For V2 protocol the SSH server is the relay.
            // Relay server authentication is done via the websocket TLS host certificate.
            // If SSH encryption/authentication is used anyway, just accept any SSH host key.
            e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
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

    private void OnRequest(object? sender, SshRequestEventArgs<SessionRequestMessage> e)
    {
        if (e.Request.RequestType == "tcpip-forward")
        {
            EventHandler<PortForwardingEventArgs>? handler = PortForwarding;
            if (handler != null)
            {
                // The event-handler has a chance to cancel forwarding of this port.
                var request = (PortForwardRequestMessage)e.Request;
                var args = new PortForwardingEventArgs((int)request.Port);
                handler(this, args);
                e.IsAuthorized = !args.Cancel;
            }
            else
            {
                e.IsAuthorized = true;
            }
        }
        else if (e.Request.RequestType == "cancel-tcpip-forward")
        {
            e.IsAuthorized = true;
        }
    }

    /// <inheritdoc />
    protected override async Task DisposeConnectionAsync()
    {
        // Invoke SshSessionClosed event proactively because if the session has not connected,
        // closing it is a no-op and won't trigger OnSshSessionClosed event.
        if (IsSshSessionActive)
        {
            OnSshSessionClosed(null);
        }

        SshSessionClosed = null;
        await base.DisposeConnectionAsync();
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

    /// <inheritdoc />
    public async Task RefreshPortsAsync(CancellationToken cancellation)
    {
        var session = this.SshSession;
        if (session == null || session.IsClosed)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var request = new SessionRequestMessage
        {
            RequestType = TunnelHost.RefreshPortsRequestType,
            WantReply = true,
        };
        await session.RequestAsync(request, cancellation);
    }

    /// <inheritdoc/>
    protected override void OnSshSessionClosed(object? sender, SshSessionClosedEventArgs e)
    {
        OnSshSessionClosed(e.Exception);
        base.OnSshSessionClosed(sender, e);
    }

    /// <inheritdoc/>
    protected override void UnsubscribeSessionEvents(SshClientSession session)
    {
        base.UnsubscribeSessionEvents(session);
        session.Authenticating -= OnSshServerAuthenticating;
        session.Disconnected -= OnSshSessionDisconnected;
        session.Request -= OnRequest;

        var pfs = session.GetService<PortForwardingService>();
        if (pfs != null)
        {
            pfs.ForwardedPortConnecting -= OnForwardedPortConnecting;
        }
    }

    /// <inheritdoc/>
    protected override void SubscribeSessionEvents(SshClientSession session)
    {
        base.SubscribeSessionEvents(session);
        session.Authenticating += OnSshServerAuthenticating;
        session.Disconnected += OnSshSessionDisconnected;
        session.Closed += OnSshSessionClosed;
        session.Request += OnRequest;

        if (ConnectionProtocol == WebSocketSubProtocolV2)
        {
            PortForwardingService pfs = session.GetService<PortForwardingService>()
                ?? throw new InvalidOperationException($"{nameof(PortForwardingService)} is not activated.");

            pfs.ForwardedPortConnecting += OnForwardedPortConnecting;

            // PortAdded and PortUpdated event handlers use a local closure
            // and cannot be unsubscribed in UnsubscribeSessionEvents().
            // To unsubscribe from them, use PortForwardingService.Disposed event handler.
            pfs.RemoteForwardedPorts.PortAdded += OnPortAddedOrUpdated;
            pfs.RemoteForwardedPorts.PortUpdated += OnPortAddedOrUpdated;
            pfs.Disposed += OnDisposed;

            void OnPortAddedOrUpdated(object? sender, ForwardedPortEventArgs e) =>
                OnForwardedPortAdded(pfs, e);

            void OnDisposed(object? sender, EventArgs e)
            {
                pfs.Disposed -= OnDisposed;
                pfs.RemoteForwardedPorts.PortAdded -= OnPortAddedOrUpdated;
                pfs.RemoteForwardedPorts.PortUpdated -= OnPortAddedOrUpdated;
            }
        }
    }
}
