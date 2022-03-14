using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Ssh;
using Microsoft.VisualStudio.Ssh.Algorithms;
using Microsoft.VisualStudio.Ssh.Events;
using Microsoft.VisualStudio.Ssh.Tcp;
using Microsoft.VsSaaS.TunnelService.Contracts;
using Microsoft.VsSaaS.TunnelService.Test.Mocks;
using Nerdbank.Streams;
using Xunit;

namespace Microsoft.VsSaaS.TunnelService.Test;

using static TcpUtils;

public class TunnelHostAndClientTests
{
    private const string MockHostRelayUri = "ws://localhost/tunnel/host";
    private const string MockClientRelayUri = "ws://localhost/tunnel/client";

    private static readonly TraceSource TestTS =
        new TraceSource(nameof(TunnelHostAndClientTests));
    private static readonly TimeSpan Timeout = Debugger.IsAttached ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(10);
    private readonly CancellationToken TimeoutToken = new CancellationTokenSource(Timeout).Token;

    private readonly Stream serverStream;
    private readonly Stream clientStream;
    private readonly IKeyPair serverSshKey;

    static TunnelHostAndClientTests()
    {
        // Enabling tracing to debug console.
        TestTS.Switch.Level = SourceLevels.All;
    }

    public TunnelHostAndClientTests()
    {
        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        this.serverSshKey = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();
    }

    private Tunnel CreateRelayTunnel(params int[] ports)
    {
        return new Tunnel
        {
            TunnelId = "test",
            ClusterId = "localhost",
            AccessTokens = new Dictionary<string, string>
            {
                [TunnelAccessScopes.Host] = "mock-host-token",
                [TunnelAccessScopes.Connect] = "mock-connect-token",
            },
            Endpoints = new[]
            {
                new TunnelRelayTunnelEndpoint
                {
                    ConnectionMode = TunnelConnectionMode.TunnelRelay,
                    ClientRelayUri = MockClientRelayUri,
                }
            },
            Ports = ports.Select((p) => new TunnelPort
            {
                PortNumber = (ushort)p,
            }).ToArray(),
        };
    }

    private SshServerSession CreateSshServerSession()
    {
        var sshConfig = new SshSessionConfiguration();
        sshConfig.AddService(typeof(PortForwardingService));
        var sshSession = new SshServerSession(sshConfig, TestTS);

        sshSession.Credentials = new[] { this.serverSshKey };
        sshSession.Authenticating += (sender, e) =>
        {
            // SSH client authentication is not yet implemented, so for now only the
            // "none" authentication type is supported.
            if (e.AuthenticationType == SshAuthenticationType.ClientNone)
            {
                e.AuthenticationTask = Task.FromResult(new ClaimsPrincipal());
            }
        };

        return sshSession;
    }

    private SshClientSession CreateSshClientSession()
    {
        var sshConfig = new SshSessionConfiguration();
        sshConfig.AddService(typeof(PortForwardingService));
        var sshSession = new SshClientSession(sshConfig, TestTS);

        sshSession.Authenticating += (sender, e) =>
        {
            // SSH server (host public key) authentication is not yet implemented.
            e.AuthenticationTask = Task.FromResult(new ClaimsPrincipal());
        };
        sshSession.Request += (sender, e) =>
        {
            e.IsAuthorized = (e.Request.RequestType == "tcpip-forward" ||
                e.Request.RequestType == "cancel-tcpip-forward");
        };

        return sshSession;
    }

    /// <summary>
    /// Connects a relay client to a duplex stream and returns the SSH server session
    /// on the other end of the stream.
    /// </summary>
    private async Task<SshServerSession> ConnectRelayClientAsync(
        TunnelRelayTunnelClient relayClient, Tunnel tunnel)
    {
        var sshSession = CreateSshServerSession();
        var serverConnectTask = sshSession.ConnectAsync(
            this.serverStream, CancellationToken.None);

        relayClient.StreamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelClient.WebSocketSubProtocol, this.clientStream);
        await relayClient.ConnectAsync(tunnel, hostId: null, CancellationToken.None)
            .WithTimeout(Timeout);

        await serverConnectTask.WithTimeout(Timeout);

        return sshSession;
    }

    /// <summary>
    /// Connects a relay host to a duplex stream and returns the multi-channel stream
    /// (SSH session wrapper) on the other end of the duplex stream.
    /// </summary>
    private async Task<MultiChannelStream> StartRelayHostAsync(
        TunnelRelayTunnelHost relayHost,
        Tunnel tunnel)
    {
        var multiChannelStream = new MultiChannelStream(this.serverStream);
        var serverConnectTask = multiChannelStream.ConnectAsync(CancellationToken.None);

        relayHost.StreamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelHost.WebSocketSubProtocol, this.clientStream);
        await relayHost.StartAsync(tunnel, CancellationToken.None).WithTimeout(Timeout);

        await serverConnectTask.WithTimeout(Timeout);

        return multiChannelStream;
    }


    [Fact]
    public async Task ConnectRelayClient()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        Assert.Collection(relayClient.ConnectionModes, new Action<TunnelConnectionMode>[]
        {
            (m) => Assert.Equal(TunnelConnectionMode.TunnelRelay, m),
        });

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
    }

    [Fact]
    public async Task ConnectRelayClientAddPort()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        var pfs = serverSshSession.ActivateService<PortForwardingService>();

        var testPort = GetAvailableTcpPort();
        using var remotePortStreamer = await pfs.StreamFromRemotePortAsync(
            IPAddress.Loopback, testPort, CancellationToken.None);
        Assert.NotNull(remotePortStreamer);
        Assert.Equal(testPort, remotePortStreamer.RemotePort);

        var streamOpenedCompletion = new TaskCompletionSource();
        remotePortStreamer.StreamOpened += (sender, stream) =>
        {
            stream.Close();
            streamOpenedCompletion.TrySetResult();
        };

        using var testClient = new TcpClient();
        await testClient.ConnectAsync(IPAddress.Loopback, testPort);
        await streamOpenedCompletion.Task.WithTimeout(Timeout);
    }

    [Fact]
    public async Task ConnectRelayClientAddPortInUse()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        var pfs = serverSshSession.ActivateService<PortForwardingService>();

        var testPort = GetAvailableTcpPort();
        var conflictListener = new TcpListener(IPAddress.Loopback, testPort);
        try
        {
            conflictListener.Start();

            using var remotePortStreamer = await pfs.StreamFromRemotePortAsync(
                IPAddress.Loopback, testPort, CancellationToken.None);
            Assert.NotNull(remotePortStreamer);
            Assert.NotEqual(testPort, remotePortStreamer.RemotePort);

            // The next available port number should have been selected.
            Assert.Equal(testPort + 1, remotePortStreamer.RemotePort);
        }
        finally
        {
            conflictListener.Stop();
        }
    }

    [Fact]
    public async Task ConnectRelayClientRemovePort()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        var pfs = serverSshSession.ActivateService<PortForwardingService>();

        var testPort = GetAvailableTcpPort();
        using var remotePortStreamer = await pfs.StreamFromRemotePortAsync(
            IPAddress.Loopback, testPort, CancellationToken.None);
        Assert.NotNull(remotePortStreamer);
        Assert.Equal(testPort, remotePortStreamer.RemotePort);

        // Disposing this object stops forwarding the port.
        remotePortStreamer.Dispose();

        // Now a connection attempt should fail.
        await Assert.ThrowsAsync<SocketException>(async () =>
        {
            // This might not fail immediately, because the Dispose() call above does not wait
            // for the other side to stop. But connections should start failing very shortly.
            while (true)
            {
                using var testClient = new TcpClient();
                await testClient.ConnectAsync(IPAddress.Loopback, testPort);
            }
        }).WithTimeout(Timeout);
    }


    [Fact]
    public async Task ConnectRelayClientNoLocalConnections()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS)
        {
            AcceptLocalConnectionsForForwardedPorts = false,
        };

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        var pfs = serverSshSession.ActivateService<PortForwardingService>();

        var testPort = GetAvailableTcpPort();
        var conflictListener = new TcpListener(IPAddress.Loopback, testPort);
        try
        {
            conflictListener.Start();

            var waitForForwardedPortTask = relayClient.WaitForForwardedPortAsync(testPort, TimeoutToken);
            Assert.NotNull(waitForForwardedPortTask);
            Assert.False(waitForForwardedPortTask.IsCompleted);

            using var remotePortStreamer = await pfs.StreamFromRemotePortAsync(
                IPAddress.Loopback, testPort, TimeoutToken);
            Assert.NotNull(remotePortStreamer);

            // Since there is no listener on the Relay client, it'll report the same remote port to the server SSH session.
            Assert.Equal(testPort, remotePortStreamer.RemotePort);

            await waitForForwardedPortTask.WaitAsync(TimeoutToken);
            Assert.Contains(relayClient.ForwardedPorts, p => p.LocalPort == testPort && p.RemotePort == testPort);

            var streamOpenedCompletion = new TaskCompletionSource();
            remotePortStreamer.StreamOpened += (sender, stream) =>
            {
                stream.Close();
                streamOpenedCompletion.TrySetResult();
            };

            using var clientStream = await relayClient.ConnectToForwardedPortAsync(testPort, TimeoutToken);
            Assert.NotNull(clientStream);

            await streamOpenedCompletion.Task.WaitAsync(TimeoutToken);
        }
        finally
        {
            conflictListener.Stop();
        }
    }

    [Fact]
    public async Task ConnectRelayHost()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var tunnel = CreateRelayTunnel();
        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        var pfs = clientSshSession.ActivateService<PortForwardingService>();
        await clientSshSession.ConnectAsync(clientRelayStream);
    }

    [Fact]
    public async Task ConnectRelayHostAutoAddPort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var tunnel = CreateRelayTunnel(GetAvailableTcpPort());

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        await TaskExtensions.WaitUntil(() => relayHost.RemoteForwarders.Count > 0)
            .WithTimeout(Timeout);
        var forwarder = relayHost.RemoteForwarders.Values.Single();
        var forwardedPort = tunnel.Ports.Single();
        Assert.Equal((int)forwardedPort.PortNumber, forwarder.LocalPort);
        Assert.Equal((int)forwardedPort.PortNumber, forwarder.RemotePort);
    }

    [Fact]
    public async Task ConnectRelayHostAddPort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var tunnel = CreateRelayTunnel();

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        Assert.Empty(relayHost.RemoteForwarders);

        var testPort = GetAvailableTcpPort();
        await relayHost.AddPortAsync(
            new TunnelPort { PortNumber = (ushort)testPort }, CancellationToken.None);
        var forwarder = relayHost.RemoteForwarders.Values.Single();
        var forwardedPort = tunnel.Ports.Single();
        Assert.Equal((int)forwardedPort.PortNumber, forwarder.LocalPort);
        Assert.Equal((int)forwardedPort.PortNumber, forwarder.RemotePort);
    }

    [Fact]
    public async Task ConnectRelayHostRemovePort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var testPort = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(testPort);

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        await TaskExtensions.WaitUntil(() => relayHost.RemoteForwarders.Count > 0)
            .WithTimeout(Timeout);

        await relayHost.RemovePortAsync((ushort)testPort, CancellationToken.None);
        Assert.Empty(relayHost.RemoteForwarders);
        Assert.Empty(tunnel.Ports);
    }
}
