// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { until } from './promiseUtils';
import { suite, test, params, slow, timeout } from '@testdeck/mocha';
import { MockTunnelManagementClient } from './mocks/mockTunnelManagementClient';
import {
    ForwardedPortConnectingEventArgs,
    PortForwardingService,
} from '@microsoft/dev-tunnels-ssh-tcp';
import {
    Tunnel,
    TunnelPort,
    TunnelConnectionMode,
    TunnelAccessScopes,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import {
    ConnectionStatus,
    RelayConnectionError,
    RelayErrorType,
    TunnelConnection,
    TunnelRelayTunnelClient,
    TunnelRelayTunnelHost,
} from '@microsoft/dev-tunnels-connections';
import {
    KeyPair,    
    NodeStream,
    PromiseCompletionSource,
    SshAlgorithms,
    SshAuthenticationType,
    SshClientCredentials,
    SshClientSession,
    SshServerCredentials,
    SshServerSession,
    SshSessionConfiguration,
    SshStream,
    Stream,
} from '@microsoft/dev-tunnels-ssh';
import { DuplexStream } from './duplexStream';
import * as net from 'net';
import { MockTunnelRelayStreamFactory } from './mocks/mockTunnelRelayStreamFactory';
import { TestTunnelRelayTunnelClient } from './testTunnelRelayTunnelClient';
import { TestMultiChannelStream } from './testMultiChannelStream';
import { Disposable } from 'vscode-jsonrpc';

interface TestConnection {
    relayHost: TunnelRelayTunnelHost;
    relayClient: TestTunnelRelayTunnelClient;
    managementClient: MockTunnelManagementClient;
    clientMultiChannelStream: TestMultiChannelStream;
    clientStream: SshStream | undefined;
    dispose(): Promise<void>;
    addPortOnHostAndValidateOnClient(portNumber: number): Promise<void>;
}

@suite
@slow(3000)
@timeout(10000)
export class TunnelHostAndClientTests {
    private mockHostRelayUri: string = 'ws://localhost/tunnel/host';
    private mockClientRelayUri: string = 'ws://localhost/tunnel/client';

    @slow(10000)
    @timeout(20000)
    public static async before() {}

    public static async after() {}

    private createRelayTunnel(ports?: number[], dontAddClientEndpoint?: boolean): Tunnel {
        return {
            tunnelId: 'test',
            clusterId: 'localhost',
            accessTokens: {
                [TunnelAccessScopes.Host]: 'mock-host-token',
                [TunnelAccessScopes.Connect]: 'mock-connect-token',
            },
            endpoints: dontAddClientEndpoint
                ? []
                : [
                      {
                          connectionMode: TunnelConnectionMode.TunnelRelay,
                          clientRelayUri: this.mockClientRelayUri,
                      } as TunnelRelayTunnelEndpoint,
                  ],
            ports: ports
                ? ports.map((p) => {
                      return { portNumber: p } as TunnelPort;
                  })
                : [],
        } as Tunnel;
    }

    private async createSshServerSession(serverSshKey?: KeyPair): Promise<SshServerSession> {
        let sshConfig = new SshSessionConfiguration();
        sshConfig.addService(PortForwardingService);
        let sshSession = new SshServerSession(sshConfig);

        sshSession.credentials = { publicKeys: [serverSshKey] } as SshServerCredentials;
        sshSession.onAuthenticating((e) => {
            // SSH client authentication is not yet implemented, so for now only the
            // "none" authentication type is supported.
            if (e.authenticationType === SshAuthenticationType.clientNone) {
                e.authenticationPromise = Promise.resolve({});
            }
        });

        return sshSession;
    }

    private createSshClientSession(): SshClientSession {
        let sshConfig = new SshSessionConfiguration();
        sshConfig.addService(PortForwardingService);
        let sshSession = new SshClientSession(sshConfig);

        sshSession.onAuthenticating((e) => {
            // SSH server (host public key) authentication is not yet implemented.
            e.authenticationPromise = Promise.resolve({});
        });
        sshSession.onRequest((e) => {
            e.isAuthorized =
                e.request.requestType == 'tcpip-forward' ||
                e.request.requestType == 'cancel-tcpip-forward';
        });

        return sshSession;
    }

    // Connects a relay client to a duplex stream and returns the SSH server session
    // on the other end of the stream.
    private async connectRelayClient(
        relayClient: TestTunnelRelayTunnelClient,
        tunnel: Tunnel,
        clientStreamFactory?: (stream: Stream) => Promise<{ stream: Stream, protocol: string }>,
        serverSshKey?: KeyPair,
    ): Promise<SshServerSession> {
        const [serverStream, clientStream] = await DuplexStream.createStreams();
        serverSshKey ??= await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();
        let sshSession = await this.createSshServerSession(serverSshKey);
        let serverConnectPromise = sshSession.connect(serverStream);

        relayClient.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelClient.webSocketSubProtocol,
            clientStream,
            clientStreamFactory,
        );

        assert.strictEqual(false, relayClient.isSshSessionActiveProperty);
        await relayClient.connect(tunnel, undefined);

        await serverConnectPromise;
        assert.strictEqual(true, relayClient.isSshSessionActiveProperty);

        return sshSession;
    }

    private async startRelayHost(
        relayHost: TunnelRelayTunnelHost,
        tunnel: Tunnel,
        clientStreamFactory?: (stream: Stream) => Promise<{ stream: Stream, protocol: string }>,
    ): Promise<TestMultiChannelStream> {
        const [serverStream, clientStream] = await DuplexStream.createStreams();

        let multiChannelStream = new TestMultiChannelStream(serverStream, clientStream);
        let serverConnectPromise = multiChannelStream.connect();

        relayHost.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelHost.webSocketSubProtocol, 
            clientStream,
            clientStreamFactory,
        );

        await relayHost.start(tunnel);

        await serverConnectPromise;

        return multiChannelStream;
    }

    @test
    public async connectRelayClientTest() {
        let relayClient = new TestTunnelRelayTunnelClient();
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.None);

        relayClient.connectionModes.forEach((connectionMode) => {
            assert.strictEqual(connectionMode, TunnelConnectionMode.TunnelRelay);
        });

        let sshSessionClosedEventFired = false;
        relayClient.sshSessionClosedEvent((e) => (sshSessionClosedEventFired = true));

        let tunnel = this.createRelayTunnel();
        await this.connectRelayClient(relayClient, tunnel);
        assert.strictEqual(false, sshSessionClosedEventFired);

        await relayClient.dispose();
        assert.strictEqual(false, relayClient.isSshSessionActiveProperty);
        assert.strictEqual(true, sshSessionClosedEventFired);
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async connectRelayClientRetriesOn429() {
        const relayClient = new TestTunnelRelayTunnelClient();
        const tunnel = this.createRelayTunnel();
        let firstAttempt = true;

        const connected = this.connectionStatusChanged(relayClient, ConnectionStatus.Connected);

        await this.connectRelayClient(relayClient, tunnel, async (stream) => {
            if (firstAttempt) {
                firstAttempt = false;
                throw new RelayConnectionError('error.tooManyRequests', {
                    errorType: RelayErrorType.TooManyRequests,
                    statusCode: 429,
                });
            }

            return { stream, protocol: TunnelRelayTunnelClient.webSocketSubProtocol };
        });

        assert.strictEqual(await connected, undefined);
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected);

        const disconnected = this.connectionStatusChanged(
            relayClient,
            ConnectionStatus.Disconnected,
        );
        await relayClient.dispose();
        assert.strictEqual(await disconnected, undefined);
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public connectRelayClientFailsForUnrecoverableError() {
        return this.connectRelayClientFailsForError(new Error('Unrecoverable Error'));
    }

    @test
    public connectRelayClientFailsFor403ForbiddenError() {
        const error = new RelayConnectionError('error.relayClientForbidden', {
            errorType: RelayErrorType.Unauthorized,
            statusCode: 403,
        });
        return this.connectRelayClientFailsForError(
            error,
            "Forbidden (403). Provide a fresh tunnel access token with 'connect' scope.",
        );
    }

    @test
    public connectRelayClientFailsFor401UnauthorizedError() {
        const error = new RelayConnectionError('error.relayClientUnauthorized', {
            errorType: RelayErrorType.Unauthorized,
            statusCode: 401,
        });
        return this.connectRelayClientFailsForError(
            error,
            "Not authorized (401). Provide a fresh tunnel access token with 'connect' scope.",
        );
    }

    @test
    async connectRelayClientWithValidHostKey() {
        // A good tunnel with the correct host public key.
        const serverSshKey = await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();
        const publicKeyBuffer = await serverSshKey.getPublicKeyBytes(serverSshKey.keyAlgorithmName);
        const tunnel = this.createRelayTunnel();
        tunnel.endpoints![0].hostPublicKeys = [publicKeyBuffer!.toString('base64')];

        const relayClient = new TestTunnelRelayTunnelClient();
        const serverSession = await this.connectRelayClient(relayClient, tunnel, undefined, serverSshKey);

        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected, 'Client must be connected.');
        assert.strictEqual(serverSession.isConnected, true, 'Server SSH session must be connected.');

        relayClient.dispose()
        serverSession.dispose();
    }

    @test
    async connectRelayClientWithStaleHostKey() {
        // A good tunnel with the correct host public key.
        const serverSshKey = await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();
        const publicKeyBuffer = await serverSshKey.getPublicKeyBytes(serverSshKey.keyAlgorithmName);
        const tunnel = this.createRelayTunnel();
        tunnel.endpoints![0].hostPublicKeys = [publicKeyBuffer!.toString('base64')];

        // Management client can fetch the good tunnel.
        const managementClient = new MockTunnelManagementClient();
        managementClient.tunnels = [tunnel];

        // Client tries to connect to a stale tunnel with outdated host public key.
        const staleTunnel = this.createRelayTunnel();
        staleTunnel.endpoints![0].hostPublicKeys = ['staleToken'];

        const relayClient = new TestTunnelRelayTunnelClient(managementClient);
        let isHostPublicKeyRefreshed = false;
        relayClient.connectionStatusChanged((e) => isHostPublicKeyRefreshed ||= (e.status === ConnectionStatus.RefreshingTunnelHostPublicKey));
        const serverSession = await this.connectRelayClient(relayClient, staleTunnel, undefined, serverSshKey);

        // Client should be connected after refreshing host public key.
        assert.strictEqual(isHostPublicKeyRefreshed, true, 'Client must have refreshed host public keys.');
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected, 'Client must be connected.');
        assert.strictEqual(serverSession.isConnected, true, 'Server SSH session must be connected.');

        relayClient.dispose()
        serverSession.dispose();
    }

    @test
    async connectRelayClientWithStaleHostKeyTunnelIsMissing() {
        // Management client cannot fetch the tunnel.
        const managementClient = new MockTunnelManagementClient();

        // Client tries to connect to a stale tunnel with outdated host public key.
        const staleTunnel = this.createRelayTunnel();
        staleTunnel.endpoints![0].hostPublicKeys = ['staleToken'];

        const relayClient = new TestTunnelRelayTunnelClient(managementClient);
        let isHostPublicKeyRefreshed = false;
        relayClient.connectionStatusChanged((e) => isHostPublicKeyRefreshed ||= (e.status === ConnectionStatus.RefreshingTunnelHostPublicKey));
        await assert.rejects(
            () => this.connectRelayClient(relayClient, staleTunnel), 
            (e) => (<Error>e).message === 'Failed to connect to tunnel relay. Error: SSH server authentication failed.');

        assert.strictEqual(isHostPublicKeyRefreshed, true, 'Client must have tried to refresh the host public key.');
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected, 'Client must be disconnected.');

        relayClient.dispose()
    }    

    @test
    async connectRelayClientWithStaleHostKeyNoTunnelManagementClient() {
        // Client tries to connect to a stale tunnel with outdated host public key.
        const staleTunnel = this.createRelayTunnel();
        staleTunnel.endpoints![0].hostPublicKeys = ['staleToken'];

        const relayClient = new TestTunnelRelayTunnelClient();
        let isHostPublicKeyRefreshed = false;
        relayClient.connectionStatusChanged((e) => isHostPublicKeyRefreshed ||= (e.status === ConnectionStatus.RefreshingTunnelHostPublicKey));
        await assert.rejects(
            () => this.connectRelayClient(relayClient, staleTunnel), 
            (e) => (<Error>e).message === 'Failed to connect to tunnel relay. Error: SSH server authentication failed.');

        assert.strictEqual(isHostPublicKeyRefreshed, false, 'Client must not have tried to refresh the host public key.');
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected, 'Client must be disconnected.');

        relayClient.dispose()
    }       

    private async connectRelayClientFailsForError(error: Error, expectedErrorMessage?: string) {
        const relayClient = new TestTunnelRelayTunnelClient();
        const tunnel = this.createRelayTunnel();
        const disconnectError = this.connectionStatusChanged(
            relayClient,
            ConnectionStatus.Disconnected,
        );

        // Connecting wraps error in a new error object with this error message
        const expectedConnectErrorMessage = `Failed to connect to tunnel relay. Error: ${expectedErrorMessage ??
            error.message}`;
        try {
            await this.connectRelayClient(relayClient, tunnel, () => {
                throw error;
            });
        } catch (e) {
            assert.strictEqual((e as Error).message, expectedConnectErrorMessage);
        }

        // connectionStatusChanged event and disconnectError contain the original error.
        assert.strictEqual(await disconnectError, error);
        assert.strictEqual(relayClient.disconnectError, error);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
    }

    @params({ localAddress: '0.0.0.0' })
    @params({ localAddress: '127.0.0.1' })
    @params.naming((params) => 'connectRelayClientAddPort: ' + params.localAddress)
    public async connectRelayClientAddPort({ localAddress }: { localAddress: string }) {
        const relayClient = new TestTunnelRelayTunnelClient();
        relayClient.localForwardingHostAddress = localAddress;
        relayClient.acceptLocalConnectionsForForwardedPorts = false;

        let tunnel = this.createRelayTunnel();
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9881;

        assert.strictEqual(false, relayClient.hasForwardedChannels(testPort));

        let remotePortStreamer = await pfs.streamFromRemotePort('127.0.0.1', testPort);
        assert.notStrictEqual(remotePortStreamer, null);
        assert.strictEqual(testPort, remotePortStreamer!.remotePort);

        await relayClient.waitForForwardedPort(testPort);

        let tcs = new PromiseCompletionSource<void>();
        let isStreamOpenedOnServer = false;
        remotePortStreamer?.onStreamOpened(async (stream: SshStream) => {
            isStreamOpenedOnServer = true;
            await tcs.promise;
            stream.destroy();
        });

        const forwardedStream = await relayClient.connectToForwardedPort(testPort);
        assert.notStrictEqual(null, forwardedStream);
        assert.strictEqual(true, relayClient.hasForwardedChannels(testPort));
        assert.strictEqual(true, isStreamOpenedOnServer);
        tcs.resolve();

        forwardedStream.destroy();
        remotePortStreamer?.dispose();
        await relayClient.dispose();
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async forwardedPortConnectingRetrieveStream() {
        const testPort = 9986;
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        relayHost.forwardConnectionsToLocalPorts = false;

        let hostStream = null;
        relayHost.forwardedPortConnecting((e: ForwardedPortConnectingEventArgs) => {
            if (e.port === testPort) {
                hostStream = e.stream;
            }
        });

        const tunnel = this.createRelayTunnel([testPort]);
        await managementClient.createTunnel(tunnel);
        const multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        const clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );

        const clientSshSession = this.createSshClientSession();
        const pfs = clientSshSession.activateService(PortForwardingService);
        pfs.acceptLocalConnectionsForForwardedPorts = false;
        await clientSshSession.connect(new NodeStream(clientRelayStream));

        const clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);

        await pfs.waitForForwardedPort(testPort);
        const clientStream =  await pfs.connectToForwardedPort(testPort);

        assert(clientStream);
        assert(hostStream);

        clientSshSession.dispose();
        multiChannelStream.dispose();
    }

    @test
    public async connectRelayClientAddPortInUse() {
        let relayClient = new TestTunnelRelayTunnelClient();

        let tunnel = this.createRelayTunnel([9982]);
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9982;
        const socket = new net.Socket();
        socket.connect(testPort, '127.0.0.1', async () => {
            let remotePortStreamer = await pfs.streamFromRemotePort('127.0.0.1', testPort);
            assert.notStrictEqual(remotePortStreamer, null);
            assert.notStrictEqual(testPort, remotePortStreamer?.remotePort);

            // The port number should be the same because the host does not know
            // when the client chose a different port number due to the conflict.
            assert.strictEqual(testPort, remotePortStreamer?.remotePort);
        });
        socket.destroy();
    }

    @test
    public async connectRelayClientRemovePort() {
        let relayClient = new TestTunnelRelayTunnelClient();

        let tunnel = this.createRelayTunnel();
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9983;
        let remotePortStreamer = await pfs.streamFromRemotePort('::', testPort);
        assert.notStrictEqual(remotePortStreamer, null);
        assert.strictEqual(testPort, remotePortStreamer?.remotePort);

        // Disposing this object stops forwarding the port.
        remotePortStreamer?.dispose();

        const socket = new net.Socket();
        // Now a connection attempt should fail.
        try {
            socket.connect(testPort, '127.0.0.1', async () => {});
        } catch (ex) {
        } finally {
            socket.destroy();
        }
    }

    @test
    public async connectRelayHost() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.None);

        let tunnel = this.createRelayTunnel();
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);

        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );

        let clientSshSession = this.createSshClientSession();
        let pfs = clientSshSession.activateService(PortForwardingService);
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        clientRelayStream.destroy();
        await relayHost.dispose();
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async connectRelayHostRetriesOn429() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.None);

        let tunnel = this.createRelayTunnel();

        let firstAttempt = true;

        const connected = this.connectionStatusChanged(relayHost, ConnectionStatus.Connected);

        await this.startRelayHost(relayHost, tunnel, async (stream) => {
            if (firstAttempt) {
                firstAttempt = false;
                throw new RelayConnectionError('error.tooManyRequests', {
                    errorType: RelayErrorType.TooManyRequests,
                    statusCode: 429,
                });
            }

            return { stream, protocol: TunnelRelayTunnelHost.webSocketSubProtocol };
        });

        assert.strictEqual(await connected, undefined);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Connected);

        const disconnected = this.connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        await relayHost.dispose();

        assert.strictEqual(await disconnected, undefined);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public connectRelayHostFailsForUnrecoverableError() {
        return this.connectRelayHostFailsForError(new Error('Unrecoverable Error'));
    }

    @test
    public connectRelayHostFailsFor403ForbiddenError() {
        const error = new RelayConnectionError('error.relayClientForbidden', {
            errorType: RelayErrorType.Unauthorized,
            statusCode: 403,
        });
        return this.connectRelayHostFailsForError(
            error,
            "Forbidden (403). Provide a fresh tunnel access token with 'host' scope.",
        );
    }

    @test
    public connectRelayHostFailsFor401UnauthorizedError() {
        const error = new RelayConnectionError('error.relayClientUnauthorized', {
            errorType: RelayErrorType.Unauthorized,
            statusCode: 401,
        });

        // The host will try to use tunnel management client to fetch a new tunnel access token.
        // Since the test always rejects the web socket with 401, it'll fail with the following error message.
        return this.connectRelayHostFailsForError(
            error,
            'Not authorized (401). Refreshed tunnel access token also does not work.',
        );
    }

    private async connectRelayHostFailsForError(error: Error, expectedErrorMessage?: string) {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();
        await managementClient.createTunnel(tunnel);

        const disconnectError = this.connectionStatusChanged(
            relayHost,
            ConnectionStatus.Disconnected,
        );

        // Connecting wraps error in a new error object with this error message
        const expectedConnectErrorMessage = `Failed to connect to tunnel relay. Error: ${expectedErrorMessage ??
            error.message}`;
        try {
            await this.startRelayHost(relayHost, tunnel, () => {
                throw error;
            });
        } catch (e) {
            assert.strictEqual((e as Error).message, expectedConnectErrorMessage);
        }

        // connectionStatusChanged event and disconnectError contain the original error.
        assert.strictEqual(await disconnectError, error);
        assert.strictEqual(relayHost.disconnectError, error);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async connectRelayHostAutoAddPort() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let tunnel = this.createRelayTunnel([9984]);
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        try {
            await clientSshSession.connect(new NodeStream(clientRelayStream));
            let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
            await clientSshSession.authenticate(clientCredentials);

            await until(() => relayHost.remoteForwarders.size === 1, 5000);

            assert.strictEqual(tunnel.ports!.length, 1);
            const forwardedPort = tunnel.ports![0];

            let forwarder = relayHost.remoteForwarders.get('9984');
            if (forwarder) {
                assert.strictEqual(forwardedPort.portNumber, forwarder.localPort);
                assert.strictEqual(forwardedPort.portNumber, forwarder.remotePort);
            }
        } finally {
            clientRelayStream.destroy();
            clientSshSession.dispose();
            await relayHost.dispose();
        }
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async ConnectRelayHostThenConnectRelayClientToDifferentPort_Fails() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let testPort = 9886;
        let differentPort = 9887;

        let tunnel = this.createRelayTunnel([testPort]);
        await managementClient.createTunnel(tunnel);
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );

        let clientSshSession = this.createSshClientSession();
        let pfs = clientSshSession.activateService(PortForwardingService);
        await clientSshSession.connect(new NodeStream(clientRelayStream));

        let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);
        
        await pfs.waitForForwardedPort(testPort);
        await assert.rejects(pfs.connectToForwardedPort(differentPort));

        clientSshSession.dispose();
        multiChannelStream.dispose();
    }

    @test
    public async connectRelayHostAddPort() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let tunnel = this.createRelayTunnel();
        await managementClient.createTunnel(tunnel);
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);

        await managementClient.createTunnelPort(tunnel, { portNumber: 9985 });
        await relayHost.refreshPorts();

        assert.strictEqual(tunnel.ports!.length, 1);
        const forwardedPort = tunnel.ports![0];

        let forwarder = relayHost.remoteForwarders.get('9985');
        if (forwarder) {
            assert.strictEqual(forwardedPort.portNumber, forwarder.localPort);
            assert.strictEqual(forwardedPort.portNumber, forwarder.remotePort);
        }
        clientRelayStream.destroy();
        clientSshSession.dispose();
    }

    @test
    public async connectRelayHostRemovePort() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let tunnel = this.createRelayTunnel([9986]);
        await managementClient.createTunnel(tunnel);
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        try {
            await clientSshSession.connect(new NodeStream(clientRelayStream));
            let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
            await clientSshSession.authenticate(clientCredentials);

            await until(() => relayHost.remoteForwarders.size === 1, 5000);
            await managementClient.deleteTunnelPort(tunnel, 9986);
            await relayHost.refreshPorts();

            assert.strictEqual(tunnel.ports!.length, 0);
            assert.strictEqual(relayHost.remoteForwarders.size, 1);
        } finally {
            clientSshSession.dispose();
            await relayHost.dispose();
        }
    }

    @test
    public async connectRelayClientToHostAndReconnectHost() {
        const testConnection = await this.startHostWithClientAndAddPort();
        const { relayHost, relayClient, clientMultiChannelStream } = testConnection;

        // Reconnect the tunnel host
        const reconnectedHostStream = new PromiseCompletionSource<Stream>();
        relayHost.streamFactory = MockTunnelRelayStreamFactory.from(
            reconnectedHostStream,
            TunnelRelayTunnelHost.webSocketSubProtocol);

        const reconnectedClientMultiChannelStream = new PromiseCompletionSource<
            TestMultiChannelStream
        >();
        relayClient.streamFactory = MockTunnelRelayStreamFactory.fromMultiChannelStream(
            reconnectedClientMultiChannelStream,
            TunnelRelayTunnelClient.webSocketSubProtocol,
        );

        clientMultiChannelStream.dropConnection();

        const [serverStream, clientStream] = await DuplexStream.createStreams();
        let newMultiChannelStream = new TestMultiChannelStream(serverStream, clientStream);
        let serverConnectPromise = newMultiChannelStream.connect();
        reconnectedHostStream.resolve(clientStream);
        await serverConnectPromise;

        reconnectedClientMultiChannelStream.resolve(newMultiChannelStream);

        // Add port to the tunnel host and wait for it on the client
        await testConnection.addPortOnHostAndValidateOnClient(9995);

        // Clean up
        await testConnection.dispose();
    }

    @test
    async connectRelayClientToHostAndReconnectClient() {
        const testConnection = await this.startHostWithClientAndAddPort();
        const { relayHost, relayClient, clientMultiChannelStream, clientStream } = testConnection;

        // Disconnect the tunnel client. It'll eventually reconnect.
        clientStream?.channel.dispose();

        // Add port to the tunnel host and wait for it on the client
        await testConnection.addPortOnHostAndValidateOnClient(9995);
        assert.strictEqual(clientMultiChannelStream.streamsOpened, 2);

        // Clean up
        await relayClient.dispose();
        await relayHost.dispose();
    }

    @test
    async connectRelayClientToHostAndFailToReconnectClient() {
        const testConnection = await this.startHostWithClientAndAddPort();
        const { relayClient, clientStream } = testConnection;

        // Wait for client disconnection and closed SSH session
        const disconnected = this.connectionStatusChanged(
            relayClient,
            ConnectionStatus.Disconnected,
        );
        const sshSessionClosed = new Promise<void>((resolve) => {
            let disposable: Disposable | undefined = relayClient.sshSessionClosedEvent((e) => {
                disposable?.dispose();
                resolve();
            });
        });

        // Prepare the error that will thrown on reconnection attempt
        const error = new RelayConnectionError('error.tunnelPortNotFound', {
            errorType: RelayErrorType.TunnelPortNotFound,
            statusCode: 404,
        });

        relayClient.streamFactory = MockTunnelRelayStreamFactory.throwing(error);

        // Disconnect the tunnel client. It won't reconnect when it hits 404.
        clientStream?.channel.dispose();
        await sshSessionClosed;
        assert.strictEqual(error, await disconnected);

        await testConnection.dispose();
    }

    private async startHostWithClientAndAddPort(): Promise<TestConnection> {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        managementClient.clientRelayUri = this.mockClientRelayUri;

        // Create and start tunnel host.
        const tunnel = this.createRelayTunnel([], true); // Hosting a tunnel adds the endpoint
        await managementClient.createTunnel(tunnel);
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        let clientMultiChannelStream = await this.startRelayHost(relayHost, tunnel);
        assert.strictEqual(clientMultiChannelStream.streamsOpened, 0);

        let clientStream: SshStream | undefined;
        const relayClient = new TestTunnelRelayTunnelClient();
        relayClient.streamFactory = MockTunnelRelayStreamFactory.fromMultiChannelStream(
            clientMultiChannelStream,
            TunnelRelayTunnelClient.webSocketSubProtocol,
            (s) => {
                clientStream = s;
            },
        );

        await relayClient.connect(tunnel);
        assert.strictEqual(clientMultiChannelStream.streamsOpened, 1);

        const result: TestConnection = {
            relayHost,
            relayClient,
            managementClient,
            clientMultiChannelStream,
            clientStream,
            dispose: async () => {
                await relayClient.dispose();
                await relayHost.dispose();
            },
            addPortOnHostAndValidateOnClient: async (portNumber: number) => {
                const disposables: Disposable[] = [];
                let clientPortAdded = new Promise((resolve, reject) => {
                    relayClient.forwardedPorts?.onPortAdded((e) => resolve(e.port.remotePort), disposables);
                    relayClient.connectionStatusChanged((e) => {
                        if (e.status === ConnectionStatus.Disconnected) {
                            reject(new Error('Relay client disconnected unexpectedly.'));
                        }
                    }, disposables);
                });
        
                await managementClient.createTunnelPort(relayHost.tunnel!, { portNumber });
                await relayHost.refreshPorts();
                try {
                    assert.strictEqual(await clientPortAdded, portNumber);
                } finally {
                    disposables.forEach((d) => d.dispose());
                }
            }
        };

        // Add port to the tunnel host and wait for it on the client
        await result.addPortOnHostAndValidateOnClient(9985);
        return result;
    }

    private async connectionStatusChanged(
        connection: TunnelConnection,
        expectedStatus: ConnectionStatus,
    ): Promise<Error | undefined> {
        const result = await new Promise<Error | undefined>((resolve, reject) => {
            connection.connectionStatusChanged((e) => {
                if (e.status === expectedStatus) {
                    resolve(e.disconnectError);
                }
                if (e.status === ConnectionStatus.Disconnected) {
                    reject(
                        e.disconnectError ??
                            new Error('Tunnel connection disconnected unexpectedly.'),
                    );
                }
            });
        });

        assert.strictEqual(connection.connectionStatus, expectedStatus);
        return result;
    }
}
