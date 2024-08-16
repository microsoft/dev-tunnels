using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.DevTunnels.Connections;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Tcp;
using Microsoft.DevTunnels.Ssh.Tcp.Events;
using Microsoft.DevTunnels.Test.Mocks;
using Nerdbank.Streams;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DevTunnels.Test;

using static TcpUtils;

public class TunnelHostAndClientTests : IClassFixture<LocalPortsFixture>
{
    private const string MockHostRelayUri = "ws://localhost/tunnel/host";
    private const string MockClientRelayUri = "ws://localhost/tunnel/client";

    private readonly TraceSource TestTS =
        new TraceSource(nameof(TunnelHostAndClientTests));
    private static readonly TimeSpan Timeout = Debugger.IsAttached ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(20);
    private readonly CancellationToken TimeoutToken = new CancellationTokenSource(Timeout).Token;

    private Stream serverStream;
    private Stream clientStream;
    private readonly IKeyPair serverSshKey;
    private readonly LocalPortsFixture localPortsFixture;

    public TunnelHostAndClientTests(LocalPortsFixture localPortsFixture, ITestOutputHelper output)
    {
        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        this.serverSshKey = SshAlgorithms.PublicKey.ECDsaSha2Nistp384.GenerateKeyPair();
        this.localPortsFixture = localPortsFixture;

        TestTS.Switch.Level = SourceLevels.All;
        TestTS.Listeners.Add(new XunitTraceListener(output));
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
        TunnelRelayTunnelClient relayClient,
        Tunnel tunnel,
        TunnelConnectionOptions connectionOptions = null,
        Func<string, Task<Stream>> clientStreamFactory = null,
        CancellationToken cancellation = default)
    {
        var sshSession = CreateSshServerSession();
        var serverConnectTask = sshSession.ConnectAsync(this.serverStream);

        var mockTunnelRelayStreamFactory = new MockTunnelRelayStreamFactory(
            relayClient.WebSocketSubProtocol, this.clientStream);
        if (clientStreamFactory != null)
        {
            mockTunnelRelayStreamFactory.StreamFactory = clientStreamFactory;
        }

        relayClient.StreamFactory = mockTunnelRelayStreamFactory;
        await relayClient.ConnectAsync(tunnel, connectionOptions, cancellation)
            .WithTimeout(Timeout);

        await serverConnectTask.WithTimeout(Timeout);

        return sshSession;
    }

    /// <summary>
    /// Connects a relay host to a duplex stream and returns the multi-channel stream
    /// (SSH session wrapper) on the other end of the duplex stream.
    /// </summary>
    private async Task<TestMultiChannelStream> ConnectRelayHostAsync(
        TunnelRelayTunnelHost relayHost,
        Tunnel tunnel,
        Func<string, Task<Stream>> hostStreamFactory = null,
        TunnelConnectionOptions connectionOptions = null,
        CancellationToken cancellation = default)
    {
        var multiChannelStream = new TestMultiChannelStream(this.serverStream);
        var serverConnectTask = multiChannelStream.ConnectAsync();

        var mockTunnelRelayStreamFactory = new MockTunnelRelayStreamFactory(
            relayHost.WebSocketSubProtocol, this.clientStream);
        if (hostStreamFactory != null)
        {
            mockTunnelRelayStreamFactory.StreamFactory = hostStreamFactory;
        }

        relayHost.StreamFactory = mockTunnelRelayStreamFactory;
        await relayHost.ConnectAsync(tunnel, connectionOptions, cancellation).WithTimeout(Timeout);

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
    public async Task ReportProgress()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        var progressEvents = new List<TunnelReportProgressEventArgs>();
        relayClient.ReportProgress += (sender, e) =>
        {
            progressEvents.Add(e);
        };

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);

        var firstEvent = progressEvents.First();
        Assert.Null(firstEvent.SessionNumber);
        Assert.True(firstEvent.Progress == Progress.OpeningClientConnectionToRelay.ToString());

        var lastEvent = progressEvents.Last();
        Assert.NotNull(lastEvent.SessionNumber);
        Assert.True(lastEvent.Progress == Progress.CompletedSessionAuthentication.ToString());
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
    public async Task ConnectRelayClientAfterDisconnect()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        Assert.Collection(relayClient.ConnectionModes, new Action<TunnelConnectionMode>[]
        {
            (m) => Assert.Equal(TunnelConnectionMode.TunnelRelay, m),
        });

        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        Assert.Null(relayClient.DisconnectException);

        var disconnectCompletion = new TaskCompletionSource();
        relayClient.ConnectionStatusChanged += (_, e) =>
        {
            if (e.Status == ConnectionStatus.Disconnected)
            {
                disconnectCompletion.TrySetResult();
            }
        };
        await serverSshSession.CloseAsync(SshDisconnectReason.ByApplication).WithTimeout(Timeout);
        await disconnectCompletion.Task.WithTimeout(Timeout);
        var exception = Assert.IsType<SshConnectionException>(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, exception.DisconnectReason);

        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession2 = await ConnectRelayClientAsync(relayClient, tunnel);
    }

    [Fact]
    public async Task ConnectRelayClientAfterFail()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();

        Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            throw new InvalidOperationException("Test failure.");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => ConnectRelayClientAsync(
            relayClient, tunnel, null, ConnectToRelayAsync));

        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
    }

    [Fact]
    public async Task ConnectRelayClientAfterCancel()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();
        var cancellationSource = new CancellationTokenSource();

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            cancellationSource.Cancel();
            return await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectRelayClientAsync(
            relayClient, tunnel, null, ConnectToRelayAsync, cancellationSource.Token));

        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
    }

    [Fact]
    public async Task ConnectRelayClientDispose()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            await relayClient.DisposeAsync();
            return await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => ConnectRelayClientAsync(relayClient, tunnel, null, ConnectToRelayAsync));
    }

    [Fact]
    public async Task ConnectRelayClientAfterDispose()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var tunnel = CreateRelayTunnel();

        await relayClient.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => ConnectRelayClientAsync(relayClient, tunnel));
    }

    [Theory]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.BadGateway)]
    [InlineData(false, HttpStatusCode.BadGateway)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task ConnectRelayClientRetriesOnErrorStatusCode(bool enableRetry, HttpStatusCode statusCode)
    {
        var connectionOptions = new TunnelConnectionOptions
        {
            EnableRetry = enableRetry,
        };
        var relayClient = new TunnelRelayTunnelClient(TestTS);
        var isRetryAttempted = false;
        relayClient.RetryingTunnelConnection += (_, e) =>
        {
            Assert.IsAssignableFrom<TunnelConnectionException>(e.Exception);
            Assert.Equal(TunnelRelayConnection.RetryMaxDelayMs / 2, e.Delay.TotalMilliseconds);
            e.Delay = TimeSpan.FromMilliseconds(100);
            isRetryAttempted = true;
        };

        var tunnel = CreateRelayTunnel();
        bool firstAttempt = true;

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            await Task.Yield();
            if (firstAttempt)
            {
                firstAttempt = false;
                await ThrowNotAWebSocket(statusCode);
            }

            return this.clientStream;
        }

        var serverSessionTask = ConnectRelayClientAsync(
            relayClient, tunnel, connectionOptions, ConnectToRelayAsync);
        if (enableRetry)
        {
            using var serverSshSession = await serverSessionTask;
            Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
            Assert.Null(relayClient.DisconnectException);
            Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);
            Assert.True(isRetryAttempted);
        }
        else
        {
            await Assert.ThrowsAsync<TunnelConnectionException>(() => serverSessionTask);
            Assert.IsType<TunnelConnectionException>(relayClient.DisconnectException);
            Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
            Assert.Equal(SshDisconnectReason.ServiceNotAvailable, relayClient.DisconnectReason);
            Assert.False(isRetryAttempted);
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
            await ConnectRelayClientAsync(relayClient, tunnel, null, ConnectToRelayAsync);
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
            () => ConnectRelayClientAsync(
                relayClient, tunnel, null, (_) => throw new ArgumentNullException("foobar")));
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
            () => ConnectRelayClientAsync(
                relayClient, tunnel, null, (_) => ThrowNotAWebSocket(HttpStatusCode.Forbidden)));
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
            () => ConnectRelayClientAsync(
                relayClient, tunnel, null, (_) => ThrowNotAWebSocket(HttpStatusCode.Unauthorized)));

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

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);
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
        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        await hostConnected.Task;

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        var pfs = clientSshSession.ActivateService<PortForwardingService>();
        await clientSshSession.ConnectAsync(clientRelayStream);
    }

    [Fact]
    public async Task ConnectRelayHostAfterDisconnect()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();

        using var serverSshSession = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Equal(1, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);

        var disconnectCompletion = new TaskCompletionSource();

        void OnRelayHostConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            if (e.Status == ConnectionStatus.Disconnected)
            {
                var exception = Assert.IsType<SshConnectionException>(e.DisconnectException);
                Assert.Equal(SshDisconnectReason.ProtocolError, exception.DisconnectReason);
                disconnectCompletion.TrySetResult();
            }
        };

        relayHost.ConnectionStatusChanged += OnRelayHostConnectionStatusChanged;

        await serverSshSession.CloseAsync(SshDisconnectReason.ProtocolError);

        await disconnectCompletion.Task.WithTimeout(Timeout);
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Equal(SshDisconnectReason.ProtocolError, relayHost.DisconnectReason);
        var ex = Assert.IsType<SshConnectionException>(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ProtocolError, ex.DisconnectReason);

        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession2 = await ConnectRelayHostAsync(relayHost, tunnel);

        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

        // Second connection doesn't update the endpoint because it's for the same tunnel.
        Assert.Equal(1, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);

        relayHost.ConnectionStatusChanged -= OnRelayHostConnectionStatusChanged;

        await relayHost.DisposeAsync();
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
        Assert.Equal(1, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(1, managementClient.TunnelEndpointsDeleted);
    }

    [Fact]
    public async Task DisposeRelayHostWithoutConnectionDoesntDeleteEndpoint()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        Assert.Equal(0, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);

        await relayHost.DisposeAsync();
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
        Assert.Equal(0, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);
    }

    [Fact]
    public async Task ConnectRelayHostAfterFail()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();

        Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            Assert.Equal(ConnectionStatus.Connecting, relayHost.ConnectionStatus);
            throw new InvalidOperationException("Test failure.");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => ConnectRelayHostAsync(
            relayHost, tunnel, ConnectToRelayAsync));

        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.IsType<InvalidOperationException>(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ProtocolError, relayHost.DisconnectReason);

        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession = await ConnectRelayHostAsync(relayHost, tunnel);

        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.BadGateway)]
    [InlineData(false, HttpStatusCode.BadGateway)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task ConnectRelayHostRetriesOnErrorStatusCode(bool enableRetry, HttpStatusCode statusCode)
    {
        var connectionOptions = new TunnelConnectionOptions
        {
            EnableRetry = enableRetry,
        };
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var isRetryAttempted = false;
        relayHost.RetryingTunnelConnection += (_, e) =>
        {
            Assert.IsAssignableFrom<TunnelConnectionException>(e.Exception);
            Assert.Equal(TunnelRelayConnection.RetryMaxDelayMs / 2, e.Delay.TotalMilliseconds);
            e.Delay = TimeSpan.FromMilliseconds(100);
            isRetryAttempted = true;
        };

        var tunnel = CreateRelayTunnel();
        bool firstAttempt = true;

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            await Task.Yield();
            if (firstAttempt)
            {
                firstAttempt = false;
                await ThrowNotAWebSocket(statusCode);
            }

            return this.clientStream;
        }

        var serverSessionTask = ConnectRelayHostAsync(
            relayHost, tunnel, ConnectToRelayAsync, connectionOptions);
        if (enableRetry)
        {
            using var serverSshSession = await serverSessionTask;
            Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
            Assert.Null(relayHost.DisconnectException);
            Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);
            Assert.True(isRetryAttempted);
        }
        else
        {
            await Assert.ThrowsAsync<TunnelConnectionException>(() => serverSessionTask);
            Assert.IsType<TunnelConnectionException>(relayHost.DisconnectException);
            Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
            Assert.Equal(SshDisconnectReason.ServiceNotAvailable, relayHost.DisconnectReason);
            Assert.False(isRetryAttempted);
        }
    }


    [Fact]
    public async Task ConnectRelayHostAfterTooManyConnectionsDisconnect()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();
        using var serverSshSession = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(1, managementClient.TunnelEndpointsUpdated);
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);

        var disconnectCompletion = new TaskCompletionSource();
        relayHost.ConnectionStatusChanged += (_, e) =>
        {
            if (e.Status == ConnectionStatus.Disconnected)
            {
                disconnectCompletion.TrySetResult();
            }
        };

        await serverSshSession.CloseAsync(SshDisconnectReason.TooManyConnections);
        await disconnectCompletion.Task.WithTimeout(Timeout);
        AssertDisconnectException();

        // Host reconnection after "Too Many Connections" is not allowed.
        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        await Assert.ThrowsAnyAsync<TunnelConnectionException>(
            () => ConnectRelayHostAsync(relayHost, tunnel));
        AssertDisconnectException();

        // Dispose doesn't clean up disconnect exception and reason.
        await relayHost.DisposeAsync();
        AssertDisconnectException();

        Assert.Equal(1, managementClient.TunnelEndpointsUpdated);
        // If the host was closed with "too many connections" reason, it means another host has connected
        // to that tunnel. That other host, when connecting, has overwritten the endpoint.
        // So no point in deleting it when the first host is disposed.
        Assert.Equal(0, managementClient.TunnelEndpointsDeleted);

        void AssertDisconnectException()
        {
            Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
            Assert.Equal(SshDisconnectReason.TooManyConnections, relayHost.DisconnectReason);
            var ex = Assert.IsType<SshConnectionException>(relayHost.DisconnectException);
            Assert.Equal(SshDisconnectReason.TooManyConnections, ex.DisconnectReason);
        }
    }

    [Fact]
    public async Task ConnectRelayHostAfterCancel()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        Assert.Equal(ConnectionStatus.None, relayHost.ConnectionStatus);
        var tunnel = CreateRelayTunnel();
        var cancellationSource = new CancellationTokenSource();

        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            Assert.Equal(ConnectionStatus.Connecting, relayHost.ConnectionStatus);
            cancellationSource.Cancel();
            return await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConnectRelayHostAsync(
            relayHost, tunnel, ConnectToRelayAsync, cancellation: cancellationSource.Token));

        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        (this.serverStream, this.clientStream) = FullDuplexStream.CreatePair();
        using var serverSshSession = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
    }

    [Fact]
    public async Task ConnectRelayHostDispose()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();
        Assert.Equal(ConnectionStatus.None, relayHost.ConnectionStatus);
        async Task<Stream> ConnectToRelayAsync(string accessToken)
        {
            Assert.Equal(ConnectionStatus.Connecting, relayHost.ConnectionStatus);
            await relayHost.DisposeAsync();
            Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
            return await ThrowNotAWebSocket(HttpStatusCode.TooManyRequests);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => ConnectRelayHostAsync(relayHost, tunnel, ConnectToRelayAsync));
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
    }

    [Fact]
    public async Task ConnectRelayHostAfterDispose()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();

        await relayHost.DisposeAsync();
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => ConnectRelayHostAsync(relayHost, tunnel));
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
    }

    [Fact]
    public async Task RelayHostDispose()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);
        var tunnel = CreateRelayTunnel();
        Assert.Equal(ConnectionStatus.None, relayHost.ConnectionStatus);
        using var serverSshSession = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);

        var disconnectCompletion = new TaskCompletionSource<Exception>();
        relayHost.ConnectionStatusChanged += (_, e) =>
        {
            if (e.Status == ConnectionStatus.Disconnected)
            {
                disconnectCompletion.TrySetResult(e.DisconnectException);
            }
        };

        await relayHost.DisposeAsync();
        Assert.Null(await disconnectCompletion.Task.WaitAsync(TimeoutToken));
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
        await serverSshSession.WaitUntiClosedAsync(TimeoutToken);
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
        Assert.Equal(ConnectionStatus.None, relayHost.ConnectionStatus);

        var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);

        // Create and connect tunnel client
        var relayClient = new TunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayConnection.ClientWebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    return await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                },
            }
        };

        Assert.Equal(ConnectionStatus.None, relayClient.ConnectionStatus);

        await relayClient.ConnectAsync(tunnel).WithTimeout(Timeout);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);

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

        // Prepare validation that would happen when the client and host start reconnecting.
        var clientReconnecting = relayClient.WaitForConnectionStatusAsync(
            ConnectionStatus.Connecting,
            assert: (client) =>
            {
                var ex = Assert.IsType<SshConnectionException>(client.DisconnectException);
                Assert.Equal(SshDisconnectReason.ConnectionLost, ex.DisconnectReason);
                Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);
            },
            cancellationToken: TimeoutToken);


        var hostReconnecting = relayHost.WaitForConnectionStatusAsync(
            ConnectionStatus.Connecting,
            assert: (host) =>
            {
                var ex = Assert.IsType<SshConnectionException>(host.DisconnectException);
                Assert.Equal(SshDisconnectReason.ConnectionLost, ex.DisconnectReason);
                Assert.Equal(SshDisconnectReason.ConnectionLost, relayHost.DisconnectReason);

            },
            cancellationToken: TimeoutToken);

        // Reconnect the tunnel host
        clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();

        var reconnectedHostStream = new TaskCompletionSource<Stream>();

        ((MockTunnelRelayStreamFactory)relayHost.StreamFactory).StreamFactory = async (accessToken) =>
        {
            var result = await reconnectedHostStream.Task;
            return result;
        };

        await this.serverStream.DisposeAsync();

        await Task.WhenAll(clientReconnecting, hostReconnecting);

        var (serverStream, clientStream) = FullDuplexStream.CreatePair();
        var newMultiChannelStream = new MultiChannelStream(serverStream);
        var serverConnectTask = newMultiChannelStream.ConnectAsync(CancellationToken.None);
        reconnectedHostStream.TrySetResult(clientStream);
        await serverConnectTask.WithTimeout(Timeout);

        clientMultiChannelStream.TrySetResult(newMultiChannelStream);

        await relayClient.WaitForConnectionStatusAsync(ConnectionStatus.Connected, cancellationToken: TimeoutToken);
        await relayHost.WaitForConnectionStatusAsync(ConnectionStatus.Connected, cancellationToken: TimeoutToken);

        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

        clientPortAdded = new TaskCompletionSource<int?>();
        await managementClient.CreateTunnelPortAsync(
           tunnel,
           new TunnelPort { PortNumber = this.localPortsFixture.Port1 },
           options: null,
           CancellationToken.None);
        await relayClient.RefreshPortsAsync(CancellationToken.None);
        Assert.Equal(this.localPortsFixture.Port1, await clientPortAdded.Task);
        Assert.Contains(relayClient.ForwardedPorts, p => p.RemotePort == this.localPortsFixture.Port);

        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayClient.DisconnectReason);

        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
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
        var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);
        var clientConnected = new TaskCompletionSource<SshStream>();

        // Create and connect tunnel client
        var relayClient = new TunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayConnection.ClientWebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    var result = await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                    clientConnected.TrySetResult(result);
                    return result;
                },
            }
        };

        await relayClient.ConnectAsync(tunnel).WithTimeout(Timeout);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);

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

        // Reconnect the tunnel client. The host stays connected.
        var relayClientDisconnected = new TaskCompletionSource();
        var relayClientReconnected = new TaskCompletionSource();
        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Connecting:
                    Assert.Null(args.DisconnectException);
                    var ex = Assert.IsType<SshConnectionException>(relayClient.DisconnectException);
                    Assert.Equal(SshDisconnectReason.ConnectionLost, ex.DisconnectReason);
                    Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);
                    Assert.Null(relayHost.DisconnectException);
                    Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);
                    relayClientDisconnected.TrySetResult();
                    break;

                case ConnectionStatus.Connected:
                    Assert.Null(args.DisconnectException);
                    Assert.Null(relayClient.DisconnectException);
                    Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);
                    Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);
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
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayClient.DisconnectReason);

        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
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
        var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

        var clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetResult(multiChannelStream);
        var clientConnected = new TaskCompletionSource<SshStream>();

        // Create and connect tunnel client
        var relayClient = new TestTunnelRelayTunnelClient(TestTS)
        {
            StreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayConnection.ClientWebSocketSubProtocol)
            {
                StreamFactory = async (accessToken) =>
                {
                    var result = await (await clientMultiChannelStream.Task).OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
                    clientConnected.TrySetResult(result);
                    return result;
                },
            }
        };

        await relayClient.ConnectAsync(tunnel).WithTimeout(Timeout);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);

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
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);

        // Expect disconnection
        bool reconnectStarted = false;
        var relayClientDisconnected = new TaskCompletionSource();
        var isDisconnectedFiredUnexpectedly = false;

        // Reconnection will fail with WebSocketException emulating Relay returning 404 (tunnel not found).
        // This is not recoverable and tunnel client reconnection should give up.
        var wse = new WebSocketException(WebSocketError.NotAWebSocket);
        wse.Data["HttpStatusCode"] = HttpStatusCode.NotFound;

        relayClient.ConnectionStatusChanged += (sender, args) =>
        {
            switch (args.Status)
            {
                case ConnectionStatus.Connected:
                    relayClientDisconnected.TrySetException(new Exception("Unexpected reconnection"));
                    break;

                case ConnectionStatus.Connecting:
                    Assert.Null(args.DisconnectException);
                    var ex = Assert.IsType<SshConnectionException>(relayClient.DisconnectException);
                    Assert.Equal(SshDisconnectReason.ConnectionLost, ex.DisconnectReason);
                    Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);
                    Assert.Null(relayHost.DisconnectException);
                    Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);
                    reconnectStarted = true;
                    break;

                case ConnectionStatus.Disconnected:
                    if (reconnectStarted)
                    {
                        var exception = Assert.IsType<TunnelConnectionException>(args.DisconnectException);
                        Assert.Equal(exception, relayClient.DisconnectException);
                        Assert.Equal(wse, exception.InnerException);
                        Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);
                        Assert.Null(relayHost.DisconnectException);
                        Assert.Equal(SshDisconnectReason.None, relayHost.DisconnectReason);

                        relayClientDisconnected.TrySetResult();
                    }
                    else
                    {
                        isDisconnectedFiredUnexpectedly = true;
                    }

                    break;
            }
        };

        var clientSshSessionClosed = new TaskCompletionSource();
        relayClient.SshSessionClosed += (sender, args) => clientSshSessionClosed.TrySetResult();

        clientMultiChannelStream = new TaskCompletionSource<MultiChannelStream>();
        clientMultiChannelStream.SetException(wse);

        // Disconnect the tunnel client
        await clientSshStream.Channel.CloseAsync();

        await relayClientDisconnected.Task;
        Assert.False(isDisconnectedFiredUnexpectedly);

        await clientSshSessionClosed.Task;

        Assert.IsType<TunnelConnectionException>(relayClient.DisconnectException);
        Assert.Equal(wse, relayClient.DisconnectException.InnerException);
        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);

        // Clean up
        await relayClient.DisposeAsync();
        await relayHost.DisposeAsync();

        Assert.Equal(ConnectionStatus.Disconnected, relayClient.ConnectionStatus);
        var exception = Assert.IsType<TunnelConnectionException>(relayClient.DisconnectException);
        Assert.Equal(wse, exception.InnerException);
        Assert.Equal(SshDisconnectReason.ConnectionLost, relayClient.DisconnectReason);

        Assert.Equal(ConnectionStatus.Disconnected, relayHost.ConnectionStatus);
        Assert.Null(relayHost.DisconnectException);
        Assert.Equal(SshDisconnectReason.ByApplication, relayHost.DisconnectReason);
    }

    [Fact]
    public async Task ConnectRelayHostAutoAddPort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var tunnel = CreateRelayTunnel(GetAvailableTcpPort());

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

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

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

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
    public async Task ConnectRelayHostThenConnectRelayClientsToForwardedPortStreamsThenSendData()
    {
        const int PortCount = 2;
        const int ClientConnectionCount = 50;

        var managementClient = new MockTunnelManagementClient
        {
            HostRelayUri = MockHostRelayUri,
            ClientRelayUri = MockClientRelayUri,
        };

        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        await using var listeners = new TcpListeners(PortCount, TestTS);
        var tunnel = CreateRelayTunnel(false, listeners.Ports);

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);
        Assert.Equal(ConnectionStatus.Connected, relayHost.ConnectionStatus);

        var clientStreamFactory = new MockTunnelRelayStreamFactory(TunnelRelayConnection.ClientWebSocketSubProtocol)
        {
            StreamFactory = async (accessToken) =>
            {
                return await multiChannelStream.OpenStreamAsync(TunnelRelayTunnelHost.ClientStreamChannelType);
            },
        };

        for (int clientConnection = 0; clientConnection < ClientConnectionCount; clientConnection++)
        {
            foreach (var port in listeners.Ports)
            {
                TestTS.TraceInformation("Connecting client #{0} to port {1}", clientConnection, port);

                // Create and connect tunnel client
                await using var relayClient = new TunnelRelayTunnelClient(TestTS)
                {
                    AcceptLocalConnectionsForForwardedPorts = false,
                    StreamFactory = clientStreamFactory,
                };

                Assert.Equal(ConnectionStatus.None, relayClient.ConnectionStatus);

                await relayClient.ConnectAsync(tunnel, TimeoutToken);
                Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);

                await relayClient.WaitForForwardedPortAsync(port, TimeoutToken);
                using var stream = await relayClient.ConnectToForwardedPortAsync(port, TimeoutToken);

                var actualPort = await stream.ReadIntToEndAsync(TimeoutToken);
                if (port != actualPort)
                {
                    // Debugger.Launch();
                    TestTS.TraceInformation("Client #{0} received unexpected port {1} instead of {2}", clientConnection, actualPort, port);
                }

                Assert.Equal(port, actualPort);
            }
        }
    }

    [Fact]
    public async Task ConnectRelayHostThenConnectRelayClientToDifferentPort_Fails()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var port = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(port);

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

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

        // Initialize the tunnel with a single port, then host it and connect a client.
        var testPort1 = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(testPort1);
        await managementClient.CreateTunnelAsync(tunnel, options: null, default);

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

        using var clientRelayStream = await multiChannelStream.OpenStreamAsync(
            TunnelRelayTunnelHost.ClientStreamChannelType);

        using var clientSshSession = CreateSshClientSession();
        await clientSshSession.ConnectAsync(clientRelayStream).WithTimeout(Timeout);

        // Try to refresh ports after connecting, before the client is authenticated.
        // It should do nothing even though the tunnel has one port.
        await relayHost.RefreshPortsAsync(CancellationToken.None);
        Assert.Empty(relayHost.RemoteForwarders);

        var clientCredentials = new SshClientCredentials("tunnel", password: null);
        await clientSshSession.AuthenticateAsync(clientCredentials);

        // The one port should be forwarded (asynchronously) after authentication.
        await TaskExtensions.WaitUntil(() => relayHost.RemoteForwarders.Count == 1)
            .WithTimeout(Timeout);
        Assert.Equal(relayHost.RemoteForwarders.Single().Value.LocalPort, testPort1);

        var testPort2 = GetAvailableTcpPort();
        Assert.NotEqual(testPort1, testPort2);

        // Add another port to the tunnel and check that it gets forwarded.
        await managementClient.CreateTunnelPortAsync(
            tunnel,
            new TunnelPort { PortNumber = (ushort)testPort2 },
            options: null,
            CancellationToken.None);
        await relayHost.RefreshPortsAsync(CancellationToken.None);
        Assert.Collection(
            relayHost.RemoteForwarders.Values.OrderBy(f => f.LocalPort),
            (f) =>
            {
                Assert.Equal(Math.Min(testPort1, testPort2), f.LocalPort);
                Assert.Equal(Math.Min(testPort1, testPort2), f.RemotePort);
            },
            (f) =>
            {
                Assert.Equal(Math.Max(testPort1, testPort2), f.LocalPort);
                Assert.Equal(Math.Max(testPort1, testPort2), f.RemotePort);
            });
    }

    [Fact]
    public async Task ConnectRelayHostRemovePort()
    {
        var managementClient = new MockTunnelManagementClient();
        managementClient.HostRelayUri = MockHostRelayUri;
        var relayHost = new TunnelRelayTunnelHost(managementClient, TestTS);

        var testPort = GetAvailableTcpPort();
        var tunnel = CreateRelayTunnel(testPort);

        using var multiChannelStream = await ConnectRelayHostAsync(relayHost, tunnel);

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
        relayClient.RetryingTunnelConnection += (sender, e) =>
        {
            // Do not retry on assert exceptions.
            if (e.Exception is XunitException)
            {
                e.Retry = false;
            }
        };

        var isTunnelHostPublicKeyRefreshed = false;
        relayClient.RefreshingTunnel += (sender, e) =>
        {
            Assert.Equal(relayClient, sender);
            Assert.Equal(staleTunnel, e.Tunnel);
            Assert.Equal(managementClient, e.ManagementClient);
            Assert.False(e.IncludePorts);
            Assert.Equal(ConnectionStatus.Connecting, relayClient.ConnectionStatus);
            Assert.Null(relayClient.DisconnectException);
            isTunnelHostPublicKeyRefreshed = true;
        };

        using var session = await ConnectRelayClientAsync(relayClient, staleTunnel);

        Assert.True(isTunnelHostPublicKeyRefreshed);
        Assert.Equal(ConnectionStatus.Connected, relayClient.ConnectionStatus);
        Assert.Null(relayClient.DisconnectException);
        Assert.Equal(SshDisconnectReason.None, relayClient.DisconnectReason);
        Assert.Equal(tunnel, relayClient.Tunnel);
        Assert.Equal(hostPublicKey, tunnel.Endpoints[0].HostPublicKeys[0]);
    }

    [Fact]
    public async Task ConnectRelayClientAndCancelPort()
    {
        var relayClient = new TunnelRelayTunnelClient(TestTS);

        Assert.Collection(relayClient.ConnectionModes, new Action<TunnelConnectionMode>[]
        {
            (m) => Assert.Equal(TunnelConnectionMode.TunnelRelay, m),
        });

        var tunnel = CreateRelayTunnel(new[] { 2000, 3000 });
        using var serverSshSession = await ConnectRelayClientAsync(relayClient, tunnel);
        Assert.Null(relayClient.DisconnectException);

        relayClient.PortForwarding += (_, e) =>
        {
            // Cancel forwarding of port 2000. (Allow forwarding of port 3000.)
            e.Cancel = e.PortNumber == 2000;
        };

        var forwarder = await serverSshSession.ForwardFromRemotePortAsync(IPAddress.Loopback, 2000);
        Assert.Null(forwarder); // Forarding of port 2000 should have been cancelled by the client.

        forwarder = await serverSshSession.ForwardFromRemotePortAsync(IPAddress.Loopback, 3000);
        Assert.NotNull(forwarder); // Forarding of port 3000 should NOT have been cancelled by the client.
    }

    private static Task<Stream> ThrowNotAWebSocket(HttpStatusCode statusCode)
    {
        var wse = new WebSocketException(
            WebSocketError.NotAWebSocket,
            $"The server returned status code '{statusCode:D}' when status code '101' was expected.");
        wse.Data["HttpStatusCode"] = statusCode;
        throw wse;
    }
}
