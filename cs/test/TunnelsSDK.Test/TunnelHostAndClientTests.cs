using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Connections;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Ssh.Tcp.Events;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Test.Mocks;
using Nerdbank.Streams;
using Xunit;

namespace Microsoft.DevTunnels.Test;

using static TcpUtils;

public class TunnelHostAndClientTests : IClassFixture<LocalPortsFixture>
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
    private readonly LocalPortsFixture localPortsFixture;

    static TunnelHostAndClientTests()
    {
        // Enabling tracing to debug console.
        TestTS.Switch.Level = SourceLevels.All;
    }

    public TunnelHostAndClientTests(LocalPortsFixture localPortsFixture)
    {
        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        this.serverSshKey = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();
        this.localPortsFixture = localPortsFixture;
    }

    private Tunnel CreateRelayTunnel(bool addClientEndpoint = true) => CreateRelayTunnel(addClientEndpoint, Enumerable.Empty<int>());

    private Tunnel CreateRelayTunnel(params int[] ports) => CreateRelayTunnel(addClientEndpoint: true, ports);

    private Tunnel CreateRelayTunnel(bool addClientEndpoint, IEnumerable<int> ports)
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
            Endpoints = addClientEndpoint ? new[]
            {
                new TunnelRelayTunnelEndpoint
                {
                    ConnectionMode = TunnelConnectionMode.TunnelRelay,
                    ClientRelayUri = MockClientRelayUri,
                }
            } : null,
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
        TunnelRelayTunnelClient relayClient, Tunnel tunnel, Func<string, Task<Stream>> clientStreamFactory = null)
    {
        var sshSession = CreateSshServerSession();
        var serverConnectTask = sshSession.ConnectAsync(
            this.serverStream, CancellationToken.None);

        var mockTunnelRelayStreamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelClient.WebSocketSubProtocol, this.clientStream);
        if (clientStreamFactory != null)
        {
            mockTunnelRelayStreamFactory.StreamFactory = clientStreamFactory;
        }

        relayClient.StreamFactory = mockTunnelRelayStreamFactory;
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
    public void NewRelayClientHasNoConnectionStatus()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(ConnectionStatus.None, relayClient.ConnectionStatus);
    }

    [Fact]
    public void NewRelayHostHasNoConnectionStatus()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(ConnectionStatus.None, relayHost.ConnectionStatus);
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
        Assert.Null(relayClient.DisconnectException);
    }

    [Fact]
    public async Task ConnectRelayClientRetriesOn429()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();
        bool firstAttempt = true;
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel, ConnectToRelayAsync);

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            await Task.Yield();
            if (firstAttempt)
            {
                firstAttempt = false;
                await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
            }

            return this.clientStream;
        }
    }

    [Fact]
    public async Task ConnectRelayClientCancelRetryOn429()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        relayClient.RetryingTunnelConnection += (_, e) => e.Retry = false;

        var tunnel = CreateRelayTunnel();
        var ex = await Assert.ThrowsAsync<TunnelConnectionException>(async () =>
        {
            await ConnectRelayClientAsync(relayClient, tunnel, ConnectToRelayAsync);
        });

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            await Task.Yield();
            await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
            return this.clientStream;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task ConnectRelayClientFailsForUnrecoverableException()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var disconnectedException = new TaskCompletionSource<Exception>();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            if (args.Status == ConnectionStatus.Disconnected)
            {
                disconnectedException.TrySetResult(args.DisconnectException);
            }
        };

        var tunnel = CreateRelayTunnel();
        await Assert.ThrowsAsync<ArgumentNullException>(
            "foobar",
            () => ConnectRelayClientAsync(relayClient, tunnel, (_) => throw new ArgumentNullException("foobar")));
        Assert.IsType<ArgumentNullException>(await disconnectedException.Task);

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.IsType<ArgumentNullException>(relayClient.DisconnectException);
    }

    [Fact]
    public async Task ConnectRelayClientFailsFor403Forbidden()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var disconnectedException = new TaskCompletionSource<Exception>();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            if (args.Status == ConnectionStatus.Disconnected)
            {
                disconnectedException.TrySetResult(args.DisconnectException);
            }
        };

        var tunnel = CreateRelayTunnel();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => ConnectRelayClientAsync(relayClient, tunnel, (_) => ThrowNotAWebSocket(HttpStatusCode.Forbidden)));
        Assert.IsType<UnauthorizedAccessException>(await disconnectedException.Task);

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.IsType<UnauthorizedAccessException>(relayClient.DisconnectException);
    }

    [Fact]
    public async Task ConnectRelayClientFailsFor401Unauthorized()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => ConnectRelayClientAsync(relayClient, tunnel, (_) => ThrowNotAWebSocket(HttpStatusCode.Unauthorized)));

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.IsType<UnauthorizedAccessException>(relayClient.DisconnectException);
    }

    [Fact]
    public async Task ConnectRelayClientSetsConnectionStatus()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        var clientConnected = new TaskCompletionSource();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Connected:
                    clientConnected.TrySetResult();
                    break;

                case ConnectionStatus.Disconnected:
                    clientConnected.TrySetException(new Exception("Unexpected disconnection"));
                    break;
            }
        };

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        await clientConnected.Task;
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    public async Task ConnectRelayClientAddPort(string localAddress)
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        relayClient.LocalForwardingHostAddress = IPAddress.Parse(localAddress);

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
    public async Task ForwardedPortConnectingRetrieveStream() {
        var testPort = GetAvailableTcpPort();
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        SshStream hostStream = null;

        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        relayHost.ForwardConnectionsToLocalPorts = false;
        relayHost.ForwardedPortConnecting += (object sender, ForwardedPortConnectingEventArgs e) => {
            if (e.Port == testPort) {
                hostStream = e.Stream;
            }
        };

        var tunnel = CreateRelayTunnel(new int[] { testPort } );
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);
        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);


        using var clientSshSession = CreateSshClientSession();
        var pfs = clientSshSession.ActivateService<PortForwardingService>();
        pfs.AcceptLocalConnectionsForForwardedPorts = false;


        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);


        await clientSshSession.WaitForForwardedPortAsync(testPort, TimeoutToken);
        using var sshStream = await clientSshSession.ConnectToForwardedPortAsync(testPort, TimeoutToken);

        Assert.NotNull(sshStream);
        Assert.NotNull(hostStream);
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

            // The port number should be the same because the host does not know
            // when the client chose a different port number due to the conflict.
            Assert.Equal(testPort, remotePortStreamer.RemotePort);
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
        var hostConnected = new TaskCompletionSource();
        relayHost.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Connected:
                    hostConnected.TrySetResult();
                    break;

                case ConnectionStatus.Disconnected:
                    hostConnected.TrySetException(new Exception("Unexpected disconnection"));
                    break;
            }
        };

        var tunnel = CreateRelayTunnel();
        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        await hostConnected.Task;

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        var pfs = clientSshSession.ActivateService<PortForwardingService>();
        await clientSshSession.ConnectAsync(clientRelayStream);
    }

    [Fact]
    public async Task ConnectRelayClientToHostAndReconnectHost()
    {
        var managementClient = new MockTunnelManagementClient
        {
            HostRelayUri = MockHostRelayUri,
            ClientRelayUri = MockClientRelayUri,
        };

        // Create and start tunnel host
        var tunnel = CreateRelayTunnel(addClientEndpoint: false); // Hosting a tunnel adds the endpoint
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);
        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);

        // Create and connect tunnel client
        var relayClient = new TunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayTunnelClient.WebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    return await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                },
            }
        };

        await relayClient.ConnectAsync(tunnel, hostId: null, CancellationToken.None)
            .WithTimeout(Timeout);

        // Add port to the tunnel host and wait for it on the client
        var clientPortAdded = new TaskCompletionSource<int?>();
        relayClient.ForwardedPorts.PortAdded += (sender, args) => clientPortAdded.TrySetResult(args.Port.RemotePort);

        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = this.localPortsFixture.Port },
            options: null,
            CancellationToken.None);
        await relayClient.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port, await clientPortAdded.Task);

        // Reconnect the tunnel host
        clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();

        var reconnectedHostStream = new TaskCompletionSource<Stream>();

        ((MockTunnelRelayStreamFactory)relayHost.StreamFactory).StreamFactory = async (accessToken) =>
        {
            var result = await reconnectedHostStream.Task;
            return result;
        };

        await this.serverStream.DisposeAsync();
        await this.clientStream.DisposeAsync();

        var (serverStream, clientStream) = FullDuplexStream.CreatePair();
        var newMultiChannelStream = new MultiChannelStream(serverStream);
        var serverConnectTask = newMultiChannelStream.ConnectAsync(CancellationToken.None);
        reconnectedHostStream.TrySetResult(clientStream);
        await serverConnectTask.WithTimeout(Timeout);

        clientMultiChannelStream.TrySetResult(newMultiChannelStream);

        clientPortAdded = new TaskCompletionSource<int?>();
        await managementClient.CreateTunnelPortAsync(
           tunnel,
           new TunnelPort { PortNumber = this.localPortsFixture.Port1 },
           options: null,
           CancellationToken.None);
        await relayClient.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port1, await clientPortAdded.Task);
        Assert.Contains(relayClient.ForwardedPorts, p => p.RemotePort == this.localPortsFixture.Port);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();
    }

    [Fact]
    public async Task ConnectRelayClientToHostAndReconnectClient()
    {
        var managementClient = new MockTunnelManagementClient
        {
            HostRelayUri = MockHostRelayUri,
            ClientRelayUri = MockClientRelayUri,
        };

        // Create and start tunnel host
        var tunnel = CreateRelayTunnel(addClientEndpoint: false); // Hosting a tunnel adds the endpoint
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);
        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);
        var clientConnected = new TaskCompletionSource<SshStream>();

        // Create and connect tunnel client
        var relayClient = new TunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayTunnelClient.WebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    var result = await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                    clientConnected.TrySetResult(result);
                    return result;
                },
            }
        };

        await relayClient.ConnectAsync(tunnel, hostId: null, CancellationToken.None)
            .WithTimeout(Timeout);

        var clientSshStream = await clientConnected.Task;

        // Add port to the tunnel host and wait for it on the client
        var clientPortAdded = new TaskCompletionSource<int?>();
        relayClient.ForwardedPorts.PortAdded += (sender, args) =>
            clientPortAdded.TrySetResult(args.Port.RemotePort);

        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = this.localPortsFixture.Port },
            options: null,
            CancellationToken.None);
        await relayClient.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port, await clientPortAdded.Task);

        // Reconnect the tunnel client
        var relayClientDisconnected = new TaskCompletionSource();
        var relayClientReconnected = new TaskCompletionSource();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Disconnected:
                    relayClientDisconnected.TrySetResult();
                    break;

                case ConnectionStatus.Connected:
                    relayClientReconnected.TrySetResult();
                    break;
            }
        };

        await clientSshStream.Channel.CloseAsync();

        await relayClientDisconnected.Task.WithTimeout(Timeout);
        await relayClientReconnected.Task.WithTimeout(Timeout);

        clientPortAdded = new TaskCompletionSource<int?>();
        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = this.localPortsFixture.Port1 },
            options: null,
            CancellationToken.None);
        await relayClient.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port1, await clientPortAdded.Task);

        Assert.Contains(relayClient.ForwardedPorts, p => p.RemotePort == this.localPortsFixture.Port);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();
    }

    [Fact]
    public async Task ConnectRelayClientToHostAndFailToReconnectClient()
    {
        var managementClient = new MockTunnelManagementClient
        {
            HostRelayUri = MockHostRelayUri,
            ClientRelayUri = MockClientRelayUri,
        };

        // Create and start tunnel host
        var tunnel = CreateRelayTunnel(addClientEndpoint: false); // Hosting a tunnel adds the endpoint
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);
        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);
        var clientConnected = new TaskCompletionSource<SshStream>();

        // Create and connect tunnel client
        var relayClient = new TestTunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayTunnelClient.WebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    var result = await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                    clientConnected.TrySetResult(result);
                    return result;
                },
            }
        };

        await relayClient.ConnectAsync(tunnel, hostId: null, CancellationToken.None)
            .WithTimeout(Timeout);

        var clientSshStream = await clientConnected.Task;

        // Add port to the tunnel host and wait for it on the client
        var clientPortAdded = new TaskCompletionSource<int?>();
        relayClient.ForwardedPorts.PortAdded += (sender, args) =>
            clientPortAdded.TrySetResult(args.Port.RemotePort);

        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = this.localPortsFixture.Port },
            options: null,
            CancellationToken.None);
        await relayHost.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port, await clientPortAdded.Task);

        // Expect disconnection
        bool reconnectStarted = false;
        var relayClientDisconnected = new TaskCompletionSource<Exception>();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Connected:
                    relayClientDisconnected.TrySetException(new Exception("Unexpected reconnection"));
                    break;

                case ConnectionStatus.Connecting:
                    reconnectStarted = true;
                    break;

                case ConnectionStatus.Disconnected:
                    if (reconnectStarted)
                    {
                        relayClientDisconnected.TrySetResult(args.DisconnectException);
                    }
                    break;
            }
        };

        var clientSshSessionClosed = new TaskCompletionSource();
        relayClient.SshSessionClosed += (sender, args) => clientSshSessionClosed.TrySetResult();

        // Reconnection will fail with WebSocketException emulating Relay returning 404 (tunnel not found).
        // This is not recoverable and tunnel client reconnection should give up.
        var wse = new WebSocketException(WebSocketError.NotAWebSocket);
        wse.Data["HttpStatusCode"] = HttpStatusCode.NotFound;
        clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetException(wse);

        // Disconnect the tunnel client
        await clientSshStream.Channel.CloseAsync();

        var disconnectException = await relayClientDisconnected.Task;
        Assert.IsType<TunnelConnectionException>(disconnectException);
        Assert.Equal(wse, disconnectException.InnerException);

        await clientSshSessionClosed.Task;

        Assert.IsType<TunnelConnectionException>(relayClient.DisconnectException);
        Assert.Equal(wse, relayClient.DisconnectException.InnerException);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();
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
    public async Task ConnectRelayHostThenConnectRelayClientToForwardedPortStream()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var port = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(port);

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        await clientSshSession.WaitForForwardedPortAsync(port, TimeoutToken);
        using var sshStream = await clientSshSession.ConnectToForwardedPortAsync(port, TimeoutToken);
    }

    [Fact]
    public async Task ConnectRelayHostThenConnectRelayClientToDifferentPort_Fails()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var port = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(port);

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        await clientSshSession.WaitForForwardedPortAsync(port, TimeoutToken);

        var differentPort = port < 60_000 ? port + 1 : port - 1;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => clientSshSession.ConnectToForwardedPortAsync(differentPort, TimeoutToken));
    }

    [Fact]
    public async Task ConnectRelayHostAddPort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var tunnel = CreateRelayTunnel();
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);

        using var multiChannelStream = await StartRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);
        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        Assert.Empty(relayHost.RemoteForwarders);

        var testPort = GetAvailableTcpPort();
        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = (ushort)testPort },
            options: null,
            CancellationToken.None);
        await relayHost.RefreshPortsAsync(CancellationToken.None);
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

        await managementClient.DeleteTunnelPortAsync(
            tunnel,
            (ushort)testPort,
            options: null,
            CancellationToken.None);
        await relayHost.RefreshPortsAsync(CancellationToken.None);
        Assert.Empty(relayHost.RemoteForwarders);
        Assert.Empty(tunnel.Ports);
    }

    [Fact]
    public async Task ConnectClientToStaleEndpoint_RefreshesTunnel()
    {
        var tunnel = CreateRelayTunnel(addClientEndpoint: true);
        var hostPublicKey = this.serverSshKey.GetPublicKeyBytes(this.serverSshKey.KeyAlgorithmName).ToBase64();
        tunnel.Endpoints[0].HostPublicKeys = new[] { hostPublicKey };

        var managementClient = new MockTunnelManagementClient();
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);

        var staleTunnel = CreateRelayTunnel(addClientEndpoint: true);
        staleTunnel.Endpoints[0].HostPublicKeys = new[] { "StaleHostPublicKey" };
        staleTunnel.TunnelId = tunnel.TunnelId;

        var relayClient = new TunnelRelayTunnelClient(managementClient, TestTS);
        var isTunnelHostPublicKeyRefreshed = false;
        relayClient.ConnectionStatusChanged += (_, e) =>
            isTunnelHostPublicKeyRefreshed |= (e.Status == ConnectionStatus.RefreshingTunnelHostPublicKey);

        using var session = await ConnectRelayClientAsync(relayClient, staleTunnel);

        Assert.True(isTunnelHostPublicKeyRefreshed);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Equal(tunnel, relayClient.Tunnel);
        Assert.Equal(hostPublicKey, tunnel.Endpoints[0].HostPublicKeys[0]);
    }

    private static Task<Stream> ThrowNotAWebSocket(HttpStatusCode statusCode)
    {
        var wse = new WebSocketException(WebSocketError.NotAWebSocket, $"The server returned status code '{statusCode:D}' when status code '101' was expected.");
        wse.Data["HttpStatusCode"] = statusCode;
        throw wse;
    }
}
