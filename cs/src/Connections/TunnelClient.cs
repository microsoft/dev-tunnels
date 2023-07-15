// <copyright file="TunnelClientBase.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Ssh.Tcp.Events;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Connections.Messages;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Base class for clients that connect to a single host
/// </summary>
public abstract class TunnelClient : TunnelConnection, ITunnelClient
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
    /// Connection protocol used to connect to host.
    /// </summary>
    public string? ConnectionProtocol { get; protected set; }

    /// <summary>
    /// Session used to connect to host
    /// </summary>
    protected SshClientSession? SshSession { get; set; }

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
    /// SSH session reconnect is enabled only if <see cref="TunnelConnection.connector"/> is not null.
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

        // Enable V1 reconnect only if connector is set as reconnect depends on it.
        // (V2 SSH reconnect is handled by the SecureStream class.)
        var clientConfig = new SshSessionConfiguration(
            enableReconnect: this.connector != null &&
                ConnectionProtocol == TunnelRelayTunnelClient.WebSocketSubProtocol);

        if (ConnectionProtocol == TunnelRelayTunnelClient.WebSocketSubProtocolV2)
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

        // SSH authentication is required in V1 protocol, optional in V2 depending on whether the
        // session enabled key exchange (as indicated by having a session ID or not). In either case
        // a password is not required. Strong authentication was already handled by the relay
        // service via the tunnel access token used for the websocket connection.
        if (this.SshSession.SessionId != null)
        {
            var clientCredentials = new SshClientCredentials("tunnel", password: null);
            if (!await this.SshSession.AuthenticateAsync(clientCredentials))
            {
                // Server authentication happens first, and if it succeeds then it sets a principal.
                throw new SshConnectionException(
                    this.SshSession.Principal == null ?
                    "SSH server authentication failed." : "SSH client authentication failed.");
            }
        }

        ConnectionStatus = ConnectionStatus.Connected;
    }

    private void OnSshSessionDisconnected(object? sender, EventArgs e) =>
        StartReconnectTaskIfNotDisposed();

    private void ConfigurePortForwardingService()
    {
        var pfs = SshPortForwardingService;
        if (pfs == null)
        {
            return;
        }

        pfs.AcceptLocalConnectionsForForwardedPorts = this.acceptLocalConnectionsForForwardedPorts;
        if (pfs.AcceptLocalConnectionsForForwardedPorts)
        {
            pfs.TcpListenerFactory = new RetryTcpListenerFactory(this.localForwardingHostAddress);
        }

        if (ConnectionProtocol == TunnelRelayTunnelClient.WebSocketSubProtocolV2)
        {
            pfs.MessageFactory = this;
            pfs.ForwardedPortConnecting += OnForwardedPortConnecting;
            pfs.RemoteForwardedPorts.PortAdded += (_, e) => OnForwardedPortAdded(pfs, e);
            pfs.RemoteForwardedPorts.PortUpdated += (_, e) => OnForwardedPortAdded(pfs, e);
        }
    }

    private void OnForwardedPortAdded(PortForwardingService pfs, ForwardedPortEventArgs e)
    {
        var port = e.Port.RemotePort;
        if (!port.HasValue)
        {
            return;
        }

        var disconnectedStreamsCount = 0;
        lock (this.disconnectedStreams)
        {
            // If there are disconnected streams for the port, re-connect them now.
            if (this.disconnectedStreams.TryGetValue(port.Value, out var streams))
            {
                disconnectedStreamsCount = streams.Count;
            }
        }

        if (disconnectedStreamsCount > 0)
        {
            this.Trace.Verbose(
                $"Reconnecting {disconnectedStreamsCount} stream(s) to forwarded port {port}");

            for (int i = 0; i < disconnectedStreamsCount; i++)
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
        var status = ConnectionStatus;
        ConnectionStatus = ConnectionStatus.RefreshingTunnelHostPublicKey;
        try
        {
            await RefreshTunnelAsync(cancellation);
        }
        finally
        {
            ConnectionStatus = status;
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
        if (this.ConnectionProtocol == TunnelRelayTunnelClient.WebSocketSubProtocol)
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

    /// <inheritdoc />
    public async Task RefreshPortsAsync(CancellationToken cancellation)
    {
        if (SshSession == null || SshSession.IsClosed)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var request = new SessionRequestMessage
        {
            RequestType = TunnelHost.RefreshPortsRequestType,
            WantReply = true,
        };
        await SshSession.RequestAsync(request, cancellation);
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
