// <copyright file="LiveShareRelayTunnelHost.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;
using Microsoft.Cascade.Rpc;
using Microsoft.Cascade.Rpc.Json;
using Microsoft.VisualStudio.Ssh;
using Microsoft.VisualStudio.Ssh.Events;
using Microsoft.VisualStudio.Ssh.Messages;
using Microsoft.VisualStudio.Ssh.Tcp;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Tunnel host implementation that uses a Live Share session and Azure Relay
    /// to accept client connections.
    /// </summary>
    public class LiveShareRelayTunnelHost : TunnelHostBase
    {
        private CancellationTokenSource? relayListenerCancellationSource;
        private Task? relayListenerTask;
        private readonly ConcurrentBag<IRpcSession> rpcSessions;
        private string? liveShareWorkspaceId;
        private readonly string hostId;

        /// <summary>
        /// Creates a new instance of the <see cref="LiveShareRelayTunnelHost" /> class.
        /// </summary>
        public LiveShareRelayTunnelHost(ITunnelManagementClient managementClient, TraceSource trace) : base(managementClient, trace)
        {
            this.hostId = MultiModeTunnelHost.HostId;
            this.rpcSessions = new ConcurrentBag<IRpcSession>();
        }

        /// <summary>
        /// Enables overriding the Azure Relay client websocket implementation.
        /// </summary>
        public IClientWebSocketFactory? RelayWebSocketFactory { get; set; }

        private ConcurrentBag<SshChannelForwarder> PortToChannelForwarders { get; } = new ConcurrentBag<SshChannelForwarder>();

        /// <inheritdoc />
        protected override async Task StartAsync(
            Tunnel tunnel,
            string[]? hostPublicKeys,
            CancellationToken cancellation)
        {
            if (this.relayListenerTask != null)
            {
                throw new InvalidOperationException(
                    "Already hosting a tunnel. Use separate instances to host multiple tunnels.");
            }

            var endpoint = new LiveShareRelayTunnelEndpoint
            {
                HostId = this.hostId,
                HostPublicKeys = hostPublicKeys,
            };
            endpoint = await ManagementClient.UpdateTunnelEndpointAsync(
                tunnel,
                endpoint,
                options: null,
                cancellation) as LiveShareRelayTunnelEndpoint;

            var relayUri = endpoint!.RelayUri;
            if (string.IsNullOrEmpty(relayUri))
            {
                throw new InvalidOperationException(
                    "The Live Share relay endpoint URI is missing.");
            }

            liveShareWorkspaceId = endpoint.WorkspaceId;
            Tunnel = tunnel;

            var relayTrace = Trace.WithName("Relay");

            var tokenProvider = new LiveShareRelayTokenProvider(
                ManagementClient,
                tunnel,
                endpoint.HostId!,
                TunnelAccessScopes.Host,
                endpoint.RelayHostSasToken);
            var listener = new HybridConnectionListener(new Uri(relayUri), tokenProvider);

            if (RelayWebSocketFactory != null)
            {
                listener.ClientWebSocketFactory = RelayWebSocketFactory;
            }

            listener.Connecting += (sender, e) => OnListenerConnecting(relayUri, relayTrace);
            listener.Online += (sender, e) => OnListenerOnline(relayTrace);
            listener.Offline += (sender, e) => OnListenerOffline(relayTrace);

            // TODO: Add retry for relay host connections
            OnListenerConnecting(relayUri, relayTrace);

            try
            {
                await listener.OpenAsync(cancellation);
            }
            catch (RelayException rex)
            {
                throw new TunnelConnectionException("Failed to connect to tunnel relay.", rex);
            }

            this.relayListenerCancellationSource = new CancellationTokenSource();
            this.relayListenerTask = Task.Run(async () =>
            {
                try
                {
                    await AcceptRelayConnectionsAsync(
                        listener,
                        relayTrace,
                        this.relayListenerCancellationSource.Token);
                }
                catch (Exception ex)
                {
                    // Catch all exceptions in the background task,
                    // and log any exceptions other than cancellations.
                    if (!(ex is OperationCanceledException))
                    {
                        relayTrace.Error("Listener error: " + ex);
                    }
                }
                finally
                {
                    await listener.CloseAsync();
                }
            });
        }

        private void OnListenerConnecting(string relayUri, TraceSource trace)
        {
            trace.Verbose($"Relay listener connecting to {relayUri}");
        }

        private void OnListenerOnline(TraceSource trace)
        {
            trace.Verbose($"Relay listener online.");
        }

        private void OnListenerOffline(TraceSource trace)
        {
            trace.Verbose($"Relay listener offline.");
        }

        private async Task AcceptRelayConnectionsAsync(
            HybridConnectionListener listener,
            TraceSource trace,
            CancellationToken cancellation)
        {
            Assumes.NotNull(listener);

            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() =>
                {
                    listener.CloseAsync();
                });
            }

            while (true)
            {
                HybridConnectionStream stream = await listener.AcceptConnectionAsync();
                if (stream == null)
                {
                    cancellation.ThrowIfCancellationRequested();
                    break;
                }

                var relayStream = new RelayStream(stream);
                trace.Info($"Accepted incoming relay connection.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await StartSshSessionAsync(relayStream, trace);
                    }
                    catch (Exception ex)
                    {
                        trace.Error("SSH connection error: " + ex);
                    }
                });
            }
        }

        private async Task StartSshSessionAsync(Stream stream, TraceSource trace)
        {
            var serverConfig = new SshSessionConfiguration();

            // Enable port-forwarding via the SSH protocol.
            serverConfig.AddService(typeof(PortForwardingService));

            var serverSession = new SshServerSession(serverConfig, trace.WithName("Server-SSH"));
            serverSession.Credentials = new SshServerCredentials(this.HostPrivateKey);

            serverSession.Authenticating += OnSshClientAuthenticating;
            serverSession.ClientAuthenticated += OnSshClientAuthenticated;
            serverSession.ChannelOpening += OnSshChannelOpening;

            var portForwardingService = serverSession.ActivateService<PortForwardingService>();

            // All tunnel hosts and clients should disable this because they do not use it (for now) and leaving it enabled is a potential security issue.
            portForwardingService.AcceptRemoteConnectionsForNonForwardedPorts = false;

            await serverSession.ConnectAsync(stream);

            this.SshSessions.Add(serverSession);
        }

        private void OnSshClientAuthenticating(object? sender, SshAuthenticatingEventArgs e)
        {
            if (e.AuthenticationType == SshAuthenticationType.ClientNone)
            {
                // For now, the client is allowed to skip SSH authentication;
                // they must have a valid tunnel access token already to get this far.
                e.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal());
            }
            else if (e.AuthenticationType == SshAuthenticationType.ClientPassword &&
                string.IsNullOrEmpty(e.ClientUsername))
            {
                // The client is authenticating with a Live Share session token.
                // TODO: Validate the Live Share session token.
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
            var sessionId = session.SessionId;
            var pfs = session.ActivateService<PortForwardingService>();

            foreach (TunnelPort port in Tunnel!.Ports ?? Enumerable.Empty<TunnelPort>())
            {
                var forwarder = await pfs.ForwardFromRemotePortAsync(
                    IPAddress.Loopback, (int)port.PortNumber, IPAddress.Loopback.ToString(), (int)port.PortNumber, CancellationToken.None);
                if (forwarder != null && sessionId != null)
                {
                    RemoteForwarders.TryAdd(new SessionPortKey(sessionId, (ushort)forwarder.RemotePort), forwarder);
                }
            }
        }

        private void OnSshChannelOpening(object? sender, SshChannelOpeningEventArgs e)
        {
            if (e.Request is PortForwardChannelOpenMessage portForwardRequest)
            {
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
                    if (sender is SshServerSession sshSession)
                    {
                        var sessionId = sshSession.SessionId;
                        if (sessionId != null)
                        {
                            if (!RemoteForwarders.ContainsKey(new SessionPortKey(sessionId, (ushort)portForwardRequest.Port)))
                            {
                                Trace.Warning("Rejecting request to connect to non-forwarded port:" +
                                    portForwardRequest.Port);
                                e.FailureReason = SshChannelOpenFailureReason.AdministrativelyProhibited;
                            }
                        }
                        else
                        {
                            Trace.Warning("Rejecting request as session has no Id");
                            e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                        }
                    }
                    else
                    {
                        Trace.Warning("Rejecting request due to invalid sender");
                        e.FailureReason = SshChannelOpenFailureReason.ConnectFailed;
                    }
                }
                else
                {
                    Trace.Warning("Nonrecognized channel type " + portForwardRequest.ChannelType);
                    e.FailureReason = SshChannelOpenFailureReason.UnknownChannelType;
                }
            }
            else if (e.Request.ChannelType == SshChannel.SessionChannelType)
            {
                e.Channel.Request += OnSshChannelRequest;
            }
            else
            {
                Trace.Warning("Rejecting request to open non-portforwarding channel.");
                e.FailureReason = SshChannelOpenFailureReason.AdministrativelyProhibited;
            }
        }

        private void OnSshChannelRequest(
            object? sender,
            SshRequestEventArgs<ChannelRequestMessage> e)
        {
            const string streamTransportPrefix = "stream-transport-";

            if (e.RequestType == "json-rpc")
            {
                Trace.Info("Starting Live Share RPC session.");
                e.IsAuthorized = true;

                var rpcTrace = Trace.WithName("RPC");
                var rpcSession = new RpcSession(rpcTrace);
                var channelStream = new SshStream((SshChannel)sender!);
                rpcSession.Connect(new JsonRpcStream(channelStream));
                this.rpcSessions.Add(rpcSession);

                // This host implements just enough of the Live Share RPC interfaces
                // to handle port-forwarding requests from the PFA.
                rpcSession.Services.Add(new RpcDispatcher<LiveShare.IConfigurationService>(
                    LiveShare.WellKnownServices.Configuration,
                    new TunnelConfigurationService(),
                    rpcTrace));
                rpcSession.Services.Add(new RpcDispatcher<LiveShare.IWorkspaceService>(
                    LiveShare.WellKnownServices.Workspace,
                    new TunnelWorkspaceService(this, rpcSession, rpcTrace),
                    rpcTrace));

                _ = rpcSession.ProcessMessagesAsync();
            }
            else if (e.RequestType.StartsWith(streamTransportPrefix))
            {
                string streamId = e.RequestType.Substring(streamTransportPrefix.Length);
                if (!ushort.TryParse(streamId, out var portNumber))
                {
                    Trace.Warning($"Invalid stream ID: {streamId}");
                    return;
                }

                var tunnelPort = Tunnel?.Ports?.FirstOrDefault((p) => p.PortNumber == portNumber);
                if (tunnelPort == null)
                {
                    Trace.Warning($"Tunnel port not found: {streamId}");
                    return;
                }

                Trace.Info($"Opening streaming channel to port {tunnelPort.PortNumber}");
                e.IsAuthorized = true;
                _ = ForwardToTunnelPortAsync((SshChannel)sender!, tunnelPort);
            }
            else
            {
                Trace.Warning($"Rejecting unsupported channel request: {e.RequestType}");
            }
        }

        private async Task ForwardToTunnelPortAsync(SshChannel channel, TunnelPort tunnelPort)
        {
            var tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(IPAddress.Loopback, (int)tunnelPort.PortNumber);
            }
            catch (SocketException sockex)
            {
                var traceErrorMessage = $"{nameof(PortForwardingService)} forwarded channel " +
                    $"#{channel.ChannelId} connection to :{tunnelPort.PortNumber} failed: {sockex.Message}";
                Trace.Warning(traceErrorMessage);
                tcpClient.Dispose();
                _ = channel.CloseAsync();
                return;
            }

            tcpClient.Client.ConfigureSocketOptionsForSsh();

            var forwarder = new SshChannelForwarder(channel, tcpClient);
            this.PortToChannelForwarders.Add(forwarder);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            var disposeTasks = new List<Task>();
            if (this.relayListenerTask != null)
            {
                this.relayListenerCancellationSource!.Cancel();
                disposeTasks.Add(this.relayListenerTask);
            }

            while (this.PortToChannelForwarders.TryTake(out var forwarder))
            {
                forwarder.Dispose();
            }

            if (Tunnel != null)
            {
                disposeTasks.Add(ManagementClient.DeleteTunnelEndpointsAsync(Tunnel, this.hostId, TunnelConnectionMode.LiveShareRelay));
            }

            foreach (RemotePortForwarder forwarder in RemoteForwarders.Values)
            {
                forwarder.Dispose();
            }

            while (this.SshSessions.TryTake(out var sshSession))
            {
                disposeTasks.Add(sshSession.CloseAsync(SshDisconnectReason.ByApplication));
            }

            await Task.WhenAll(disposeTasks);
        }

        private class TunnelConfigurationService : LiveShare.IConfigurationService
        {
            public Task<LiveShare.AgentVersionInfo> ExchangeVersionsAsync(
                LiveShare.AgentVersionInfo agentVersion,
                LiveShare.ClientVersionInfo clientVersion,
                CancellationToken cancellationToken)
            {
                var assemblyVersion = typeof(TunnelConfigurationService).Assembly.GetName().Version;
                var liveShareProtocolVersion = new Version(2, 2);
                return Task.FromResult(new LiveShare.AgentVersionInfo
                {
                    PlatformName = "test",
                    PlatformVersion = new Version(10, 0),
                    ProtocolVersion = liveShareProtocolVersion,
                    Version = assemblyVersion,
                });
            }

            public Task ExchangeSettingsAsync(
                LiveShare.UserSettings settings,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private class TunnelWorkspaceService : LiveShare.IWorkspaceService
        {
            public TunnelWorkspaceService(
                LiveShareRelayTunnelHost host,
                IRpcSession rpcSession,
                TraceSource trace)
            {
                Host = host;
                RpcSession = rpcSession;
                Trace = trace;
            }

            private LiveShareRelayTunnelHost Host { get; }

            private IRpcSession RpcSession { get; }

            private TraceSource Trace { get; }

            public Task<LiveShare.WorkspaceSessionInfo> JoinWorkspaceAsync(
                LiveShare.WorkspaceJoinInfo joinInfo,
                CancellationToken cancellationToken)
            {
                if (joinInfo.Id != Host.liveShareWorkspaceId)
                {
                    throw new ArgumentException("Invalid workspace ID.");
                }

                // Add RPC services that the PFA will call.
                RpcSession.Services.Add(new RpcDispatcher<LiveShare.IServerSharingService>(
                    LiveShare.WellKnownServices.ServerSharing, new TunnelServerSharingService(Host), Trace));
                RpcSession.Services.Add(new RpcDispatcher<LiveShare.IStreamManagerService>(
                    LiveShare.WellKnownServices.StreamManager, new TunnelStreamManagerService(), Trace));

                // PFA ignores the returned object, so it doesn't need to be filled in.
                var sessionInfo = new LiveShare.WorkspaceSessionInfo
                {
                    Id = joinInfo.Id,
                    Sessions = new Dictionary<int, LiveShare.WorkspaceUserProfile>()
                    {
                        [0] = new LiveShare.WorkspaceUserProfile
                        {
                            Id = string.Empty,
                            IsOwner = true,
                            IsHost = true,
                        },
                    },
                };
                return Task.FromResult(sessionInfo);
            }
        }

        private class TunnelServerSharingService : LiveShare.IServerSharingService
        {
            public TunnelServerSharingService(LiveShareRelayTunnelHost host)
            {
                Host = host;
            }

            private LiveShareRelayTunnelHost Host { get; }

            public Task<LiveShare.SharedPipe[]> GetSharedPipesAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Array.Empty<LiveShare.SharedPipe>());
            }

            public Task<LiveShare.SharedServer[]> GetSharedServersAsync(CancellationToken cancellationToken)
            {
                // TODO: If the port protocol is "auto" (and port number is not 443),
                // attempt a TLS handshake with the local server and report the result here.
                Func<TunnelPort, bool> isHttps = (p) =>
                    p.Protocol == TunnelProtocol.Https ||
                    (p.Protocol == TunnelProtocol.Auto && p.PortNumber == 443);

                var sharedServers = Host.Tunnel?.Ports?.Select((p) => new LiveShare.SharedServer
                {
                    SourcePort = (int)p.PortNumber,
                    DestinationPort = (int)p.PortNumber,
                    StreamName = "pfs",
                    StreamCondition = p.PortNumber.ToString(),
                    SessionName = p.PortNumber.ToString(),
                    HasTLSHandshakePassed = isHttps(p),
                    Privacy =
                        p.AccessControl?.IsAnonymousAllowed(TunnelAccessScopes.Connect) == true ?
                        LiveShare.PrivacyEnum.Public : LiveShare.PrivacyEnum.Private,
                }).ToArray() ?? Array.Empty<LiveShare.SharedServer>();
                return Task.FromResult(sharedServers);
            }
        }

        private class TunnelStreamManagerService : LiveShare.IStreamManagerService
        {
            public Task<string?> GetStreamAsync(
                string streamName,
                string condition,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<string?>(condition);
            }
        }
    }
}
