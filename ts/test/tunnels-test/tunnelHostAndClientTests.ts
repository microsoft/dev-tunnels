// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { until, withTimeout } from './promiseUtils';
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
    TunnelReportProgressEventArgs,
} from '@microsoft/dev-tunnels-contracts';
import {
    ConnectionStatus,
    RelayConnectionError,
    RelayErrorType,
    TunnelConnection,
    TunnelRelayTunnelClient,
    TunnelRelayTunnelHost,
    maxReconnectDelayMs,
} from '@microsoft/dev-tunnels-connections';
import {
    CancellationError,
    KeyPair,    
    NodeStream,
    ObjectDisposedError,
    Progress,
    PromiseCompletionSource,
    SshAlgorithms,
    SshAuthenticationType,
    SshClientCredentials,
    SshClientSession,
    SshConnectionError,
    SshDisconnectReason,
    SshServerCredentials,
    SshServerSession,
    SshSessionConfiguration,
    SshStream,
    Stream,
} from '@microsoft/dev-tunnels-ssh';
import { DuplexStream, shutdownWebSocketServer } from './duplexStream';
import * as net from 'net';
import { MockTunnelRelayStreamFactory } from './mocks/mockTunnelRelayStreamFactory';
import { TestTunnelRelayTunnelClient } from './testTunnelRelayTunnelClient';
import { TestMultiChannelStream } from './testMultiChannelStream';
import { CancellationToken, CancellationTokenSource, Disposable } from 'vscode-jsonrpc';
import { TunnelConnectionOptions } from 'src/connections/tunnelConnectionOptions';

interface TestConnection {
    relayHost: TunnelRelayTunnelHost;
    relayClient: TestTunnelRelayTunnelClient;
    managementClient: MockTunnelManagementClient;
    clientMultiChannelStream: TestMultiChannelStream;
    clientStream: SshStream | undefined;
    dispose(clientDisconnectReason?: SshDisconnectReason): Promise<void>;
    addPortOnHostAndValidateOnClient(portNumber: number): Promise<void>;
}


const tooManyRequestsError = new RelayConnectionError(
    'error.tooManyRequests', {
        errorType: RelayErrorType.TooManyRequests,
        statusCode: 429,
    });

const badGatewayError = new RelayConnectionError(
    'error.badGateway', {
        errorType: RelayErrorType.BadGateway,
        statusCode: 502,
    });  

const serviceUnavailableError = new RelayConnectionError(
    'error.serviceUnavailable', {
        errorType: RelayErrorType.ServiceUnavailable,
        statusCode: 503,
    });    

async function connectionStatusChanged(
    connection: TunnelConnection,
    ...expectedStatus: ConnectionStatus[]
): Promise<Error | undefined> {
    const result = new Promise<Error | undefined>((resolve, reject) => {
        connection.connectionStatusChanged((e) => {
            if (e.status === expectedStatus[0]) {
                if (expectedStatus.length > 1) {
                    expectedStatus.shift();
                } else {
                    resolve(e.disconnectError);
                }
            }
        });
    });

    return await withTimeout(result, 5000);
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

    public static async after() {
        shutdownWebSocketServer();
    }

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
        options: {
            relayClient: TestTunnelRelayTunnelClient,
            tunnel: Tunnel,
            connectionOptions?: TunnelConnectionOptions,
            clientStreamFactory?: (stream: Stream) => Promise<{ stream: Stream, protocol: string }>,
            serverSshKey?: KeyPair,
            type?: string,
        },
        cancellation?: CancellationToken,
    ): Promise<SshServerSession> {
        const {relayClient, tunnel, connectionOptions, clientStreamFactory, serverSshKey, type} = options;
        const [serverStream, clientStream] = await DuplexStream.createStreams(type);
        let sshSession = await this.createSshServerSession(serverSshKey ?? await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair());
        let serverConnectPromise = sshSession.connect(serverStream);

        relayClient.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelClient.webSocketSubProtocol,
            clientStream,
            clientStreamFactory,
        );

        assert.strictEqual(relayClient.isSshSessionActiveProperty, false);
        await relayClient.connect(tunnel, connectionOptions, cancellation);

        await serverConnectPromise;
        assert.strictEqual(relayClient.isSshSessionActiveProperty, true);

        return sshSession;
    }

    private async connectRelayHost(
        options: {
            relayHost: TunnelRelayTunnelHost,
            tunnel: Tunnel,
            connectionOptions?: TunnelConnectionOptions,
            clientStreamFactory?: (stream: Stream) => Promise<{ stream: Stream, protocol: string }>,
            type?: string,
            },
        cancellation?: CancellationToken,
    ): Promise<TestMultiChannelStream> {
        const {relayHost, tunnel, connectionOptions, clientStreamFactory, type} = options;
        const [serverStream, clientStream] = await DuplexStream.createStreams(type);

        let multiChannelStream = new TestMultiChannelStream(serverStream);
        let serverConnectPromise = multiChannelStream.connect();

        relayHost.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelHost.webSocketSubProtocol, 
            clientStream,
            clientStreamFactory,
        );

        await relayHost.connect(tunnel, connectionOptions, cancellation);

        await serverConnectPromise;

        return multiChannelStream;
    }

    @test
    public async reportProgressTest() {
        let relayClient = new TestTunnelRelayTunnelClient();
        let progressEvents: TunnelReportProgressEventArgs[] = [];
        relayClient.onReportProgress((e)=> {
            progressEvents.push(e)
        });

        let tunnel = this.createRelayTunnel();
        await this.connectRelayClient({relayClient, tunnel});

        await relayClient.dispose();

        let firstEvent = progressEvents[0];
        assert.strictEqual(firstEvent.progress, Progress.OpeningClientConnectionToRelay);
        assert.notStrictEqual(firstEvent.sessionNumber, null);

        let lastEvent = progressEvents.pop() as TunnelReportProgressEventArgs;
        assert.strictEqual(lastEvent.progress, Progress.CompletedSessionAuthentication);
        assert.notEqual(lastEvent.sessionNumber, null);
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
        await this.connectRelayClient({relayClient, tunnel});
        assert.strictEqual(sshSessionClosedEventFired, false);

        await relayClient.dispose();
        assert.strictEqual(relayClient.isSshSessionActiveProperty, false);
        assert.strictEqual(sshSessionClosedEventFired, true);
        assert.strictEqual(relayClient.disconnectError, undefined);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
    }

    @test
    public async connectRelayClientAfterDisconnect() {
        const tunnel = this.createRelayTunnel();
        const relayClient = new TestTunnelRelayTunnelClient();
        try {
            // Use web socket to ensure SSH disconnect message is not preempted by stream closure in DuplexStream.
            const serverSshSession = await this.connectRelayClient({relayClient, tunnel, type: 'ws'});
            assert(!relayClient.disconnectError);

            const disconnected = connectionStatusChanged(relayClient, ConnectionStatus.Disconnected);
            await serverSshSession.close(SshDisconnectReason.byApplication);
            await disconnected;

            assert.equal(relayClient.disconnectReason, SshDisconnectReason.byApplication);
            assert(relayClient.disconnectError! instanceof SshConnectionError);
            assert.equal((<SshConnectionError>relayClient.disconnectError).reason, SshDisconnectReason.byApplication);

            await this.connectRelayClient({relayClient, tunnel});
        } finally {
            relayClient.dispose();
        }
    }

    @test
    public async connectRelayClientAfterFail() {
        const tunnel = this.createRelayTunnel();
        const relayClient = new TestTunnelRelayTunnelClient();
        try {
            
            let error: Error | undefined = undefined;
            try {
                await this.connectRelayClient({relayClient, tunnel, clientStreamFactory: (s) => {
                    throw new Error('Test error');
                }});
            } catch (e) {
                error = <Error>e;
            }
            assert(error?.message?.includes('Test error'));

            await this.connectRelayClient({relayClient, tunnel});
        } finally {
            relayClient.dispose();
        }
    }

    @test
    public async connectRelayClientAfterCancel() {
        const tunnel = this.createRelayTunnel();
        const relayClient = new TestTunnelRelayTunnelClient();
        try {
            const cancellationSource = new CancellationTokenSource();
            let error: Error | undefined = undefined;
            try {
                await this.connectRelayClient({relayClient, tunnel, clientStreamFactory: (s) => {
                    cancellationSource.cancel();
                    throw new RelayConnectionError(
                        'error.tooManyRequests',
                        { errorType: RelayErrorType.ConnectionError, statusCode: 429 },
                    );
                }}, cancellationSource.token);
            } catch (e) {
                error = <Error>e;
            }
            assert(error instanceof CancellationError);

            await this.connectRelayClient({relayClient, tunnel});
        } finally {
            relayClient.dispose();
        }
    }

    @test
    public async connectRelayClientDispose() {
        const tunnel = this.createRelayTunnel();
        const relayClient = new TestTunnelRelayTunnelClient();
        let error: Error | undefined = undefined;
        try {
            await this.connectRelayClient({relayClient, tunnel, clientStreamFactory: (s) => {
                relayClient.dispose();
                throw new RelayConnectionError(
                    'error.tooManyRequests',
                    { errorType: RelayErrorType.ConnectionError, statusCode: 429 },
                );
            }});
        } catch (e) {
            error = <Error>e;
        }
        assert(error instanceof ObjectDisposedError);
    }

    @test
    public async connectRelayClientAfterDispose() {
        const tunnel = this.createRelayTunnel();
        const relayClient = new TestTunnelRelayTunnelClient();
        relayClient.dispose();

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayClient({relayClient, tunnel});
        } catch (e) {
            error = <Error>e;
        }
        assert(error instanceof ObjectDisposedError);
   }

    @test
    @params({ enableRetry: true, error: tooManyRequestsError})
    @params({ enableRetry: false, error: tooManyRequestsError})
    @params({ enableRetry: true, error: badGatewayError })
    @params({ enableRetry: false, error: badGatewayError })    
    @params({ enableRetry: true, error: serviceUnavailableError })
    @params({ enableRetry: false, error: serviceUnavailableError })    
    @params.naming((params) => `connectRelayClientRetriesOnErrorStatusCode(enableRetry: ${params.enableRetry}, statusCode: ${params.error.errorContext.statusCode})`)
    public async connectRelayClientRetriesOnErrorStatusCode(connectionOptions: TunnelConnectionOptions & {error: RelayConnectionError}) {
        const relayClient = new TestTunnelRelayTunnelClient();
        let isRetryAttempted = false;
        relayClient.retryingTunnelConnection((e) => {
            assert.equal(e.delayMs, maxReconnectDelayMs/2);
            assert(e.error instanceof RelayConnectionError);
            e.delayMs = 100;
            isRetryAttempted = true;
        });

        const tunnel = this.createRelayTunnel();
        let firstAttempt = true;

        const connected = connectionStatusChanged(relayClient, ConnectionStatus.Connected);
        const disconnected = connectionStatusChanged(relayClient, ConnectionStatus.Disconnected);

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayClient({relayClient, tunnel, connectionOptions, clientStreamFactory: async (stream) => {
                if (firstAttempt) {
                    firstAttempt = false;
                    throw connectionOptions.error;
                }

                return { stream, protocol: TunnelRelayTunnelClient.webSocketSubProtocol };
            }});
        } catch (e) {
            error = <Error>e;
        }

        if (connectionOptions.enableRetry) {
            assert(isRetryAttempted);
            assert(!error);
            assert.strictEqual(await connected, undefined);
            assert.strictEqual(relayClient.disconnectError, undefined);
            assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected);
            assert.strictEqual(relayClient.disconnectReason, undefined);

            await relayClient.dispose();
            assert.strictEqual(await disconnected, undefined);
            assert.strictEqual(relayClient.disconnectError, undefined);
            assert.strictEqual(relayClient.disconnectReason, SshDisconnectReason.byApplication);
            assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
        } else {
            assert(!isRetryAttempted);
            assert(error?.message?.includes(connectionOptions.error.message));
            assert.strictEqual(await disconnected, connectionOptions.error);
            assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
            assert.strictEqual(relayClient.disconnectReason, SshDisconnectReason.serviceNotAvailable);

            await relayClient.dispose();
            assert.strictEqual(relayClient.disconnectError, connectionOptions.error);
            assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
            assert.strictEqual(relayClient.disconnectReason, SshDisconnectReason.serviceNotAvailable);
        }
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
            SshDisconnectReason.authCancelledByUser,
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
            SshDisconnectReason.authCancelledByUser,
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
        const serverSession = await this.connectRelayClient({relayClient, tunnel, serverSshKey});

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
        relayClient.refreshingTunnel((e) => {
            assert.strictEqual(e.tunnel, staleTunnel);
            assert.strictEqual(e.managementClient, managementClient);
            assert.strictEqual(e.includePorts, false);
            assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connecting);
            assert(!relayClient.disconnectError);
            assert(!relayClient.disconnectReason);
            isHostPublicKeyRefreshed = true;
        });

        const serverSession = await this.connectRelayClient({relayClient, tunnel: staleTunnel, serverSshKey});

        // Client should be connected after refreshing host public key.
        assert.strictEqual(isHostPublicKeyRefreshed, true, 'Client must have refreshed host public keys.');
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected, 'Client must be connected.');
        assert(!relayClient.disconnectError);
        assert(!relayClient.disconnectReason);
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
        relayClient.refreshingTunnel(() => isHostPublicKeyRefreshed = true);
        await assert.rejects(
            () => this.connectRelayClient({relayClient, tunnel: staleTunnel}), 
            (e) => (<Error>e).message === 'SSH server authentication failed.');

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
        relayClient.refreshingTunnel(() => isHostPublicKeyRefreshed = true);
        await assert.rejects(
            () => this.connectRelayClient({relayClient, tunnel: staleTunnel}), 
            (e) => (<Error>e).message === 'SSH server authentication failed.');

        assert.strictEqual(isHostPublicKeyRefreshed, true, 'Client must have tried to refresh the host public key.');
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected, 'Client must be disconnected.');

        relayClient.dispose()
    }       

    private async connectRelayClientFailsForError(error: Error, expectedErrorMessage?: string, expectedDisconnectReason?: SshDisconnectReason) {
        const relayClient = new TestTunnelRelayTunnelClient();
        const tunnel = this.createRelayTunnel();
        const disconnectError = connectionStatusChanged(
            relayClient,
            ConnectionStatus.Disconnected,
        );

        try {
            await this.connectRelayClient({relayClient, tunnel, clientStreamFactory: () => {
                throw error;
            }});
        } catch (e) {
            assert.strictEqual((e as Error).message, expectedErrorMessage ?? error.message);
        }

        // connectionStatusChanged event and disconnectError contain the original error.
        assert.strictEqual(await disconnectError, error);
        assert.strictEqual(relayClient.disconnectError, error);
        assert.strictEqual(relayClient.disconnectReason, expectedDisconnectReason || SshDisconnectReason.connectionLost);
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
        let serverSshSession = await this.connectRelayClient({relayClient, tunnel});
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9881;

        assert.strictEqual(relayClient.hasForwardedChannels(testPort), false);

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
        assert.notStrictEqual(forwardedStream, null);
        assert.strictEqual(relayClient.hasForwardedChannels(testPort), true);
        assert.strictEqual(isStreamOpenedOnServer, true);
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
        const multiChannelStream = await this.connectRelayHost({relayHost, tunnel});
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
        const relayClient = new TestTunnelRelayTunnelClient();

        const testPort = 9982;
        const tunnel = this.createRelayTunnel([testPort]);
        const serverSshSession = await this.connectRelayClient({relayClient, tunnel});
        const pfs = serverSshSession.activateService(PortForwardingService);

        const connectCompletion = new PromiseCompletionSource<void>();
        const conflictListener = new net.Server();
        conflictListener.listen(testPort, '127.0.0.1', async () => {
            let remotePortStreamer = await pfs.streamFromRemotePort('127.0.0.1', testPort);

            // The port number should be the same because the host does not know
            // when the client chose a different port number due to the conflict.
            assert.strictEqual(testPort, remotePortStreamer?.remotePort);

            connectCompletion.resolve();
        });

        try {
            await withTimeout(connectCompletion.promise, 5000);
        } finally {
            conflictListener.close();
            relayClient.dispose();
        }
    }

    @test
    public async connectRelayClientRemovePort() {
        let relayClient = new TestTunnelRelayTunnelClient();

        let tunnel = this.createRelayTunnel();
        let serverSshSession = await this.connectRelayClient({relayClient, tunnel});
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9983;
        let remotePortStreamer = await pfs.streamFromRemotePort('::', testPort);
        assert.notStrictEqual(remotePortStreamer, null);
        assert.strictEqual(remotePortStreamer?.remotePort, testPort);

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
    public async connectRelayHostTest() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.None);

        let tunnel = this.createRelayTunnel();
        let multiChannelStream = await this.connectRelayHost({relayHost, tunnel});

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
    public async connectRelayHostAfterDisconnect() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();

        const disconnected = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        const hostStream = await this.connectRelayHost({relayHost, tunnel});
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert.equal(1, managementClient.tunnelEndpointsUpdated);
        assert.equal(0, managementClient.tunnelEndpointsDeleted);

        hostStream.dispose();
        await disconnected;

        await this.connectRelayHost({relayHost, tunnel});
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);

        // Second connection doesn't update the endpoint because it's for the same tunnel.
        assert.equal(1, managementClient.tunnelEndpointsUpdated);
        assert.equal(0, managementClient.tunnelEndpointsDeleted);

        const disconnectedOnDispose = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        await relayHost.dispose();
        await disconnectedOnDispose;
        assert(!relayHost.disconnectError);
        assert.equal(relayHost.disconnectReason, SshDisconnectReason.byApplication);

        // Disposal deletes the endpoint.
        assert.equal(1, managementClient.tunnelEndpointsUpdated);
        assert.equal(1, managementClient.tunnelEndpointsDeleted);
    }

    @test
    public async disposeRelayHostWithoutConnectionDoesntDeleteEndpoint() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.equal(0, managementClient.tunnelEndpointsUpdated);
        assert.equal(0, managementClient.tunnelEndpointsDeleted);

        const disconnectedOnDispose = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        await relayHost.dispose();
        await disconnectedOnDispose;
        assert(!relayHost.disconnectError);
        assert.equal(relayHost.disconnectReason, SshDisconnectReason.byApplication);

        // Disposal doesn't delete the endpoint because it was not created.
        assert.equal(0, managementClient.tunnelEndpointsUpdated);
        assert.equal(0, managementClient.tunnelEndpointsDeleted);
    }

    @test
    public async connectRelayHostAfterFail() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayHost({relayHost, tunnel, clientStreamFactory: (_) => {
                throw new Error('Test failure');
            }});
        } catch (e) {
            error = <Error>e;
        }
        assert(error?.message?.includes('Test failure'));

        await this.connectRelayHost({relayHost, tunnel});
    }

    @test
    public async connectRelayHostAfterTooManyConnectionsDisconnect() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();

        const hostStream = await this.connectRelayHost({relayHost, tunnel, type: 'ws'});
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert.equal(1, managementClient.tunnelEndpointsUpdated);
        assert.equal(0, managementClient.tunnelEndpointsDeleted);

        const disconnected = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);

        hostStream.close(SshDisconnectReason.tooManyConnections);
        const error = <SshConnectionError>await disconnected;
        assert.equal(error?.reason, SshDisconnectReason.tooManyConnections);
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert.equal(relayHost.disconnectReason, SshDisconnectReason.tooManyConnections);
        assert.equal((<SshConnectionError>relayHost.disconnectError).reason, SshDisconnectReason.tooManyConnections);

        let reconnectError;
        try {
            await this.connectRelayHost({relayHost, tunnel});
        } catch (e) {
            reconnectError = e;
        }

        assert.equal((<SshConnectionError>reconnectError).reason, SshDisconnectReason.tooManyConnections);
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert.equal(relayHost.disconnectReason, SshDisconnectReason.tooManyConnections);
        assert.equal((<SshConnectionError>relayHost.disconnectError).reason, SshDisconnectReason.tooManyConnections);

        await relayHost.dispose();
        assert.equal(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert.equal(relayHost.disconnectReason, SshDisconnectReason.tooManyConnections);
        assert.equal((<SshConnectionError>relayHost.disconnectError).reason, SshDisconnectReason.tooManyConnections);

        assert.equal(1, managementClient.tunnelEndpointsUpdated);

        // If the host was closed with "too many connections" reason, it means another host has connected
        // to that tunnel. That other host, when connecting, has overwritten the endpoint.
        // So no point in deleting it when the first host is disposed.        
        assert.equal(0, managementClient.tunnelEndpointsDeleted);
    }

    @test
    public async connectRelayHostAfterCancel() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();
        const cancellationSource = new CancellationTokenSource();

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayHost({relayHost, tunnel, clientStreamFactory: (_) => {
                cancellationSource.cancel();
                throw new RelayConnectionError(
                    'error.tooManyRequests',
                    { errorType: RelayErrorType.ConnectionError, statusCode: 429 },
                );
            }}, cancellationSource.token);
        } catch (e) {
            error = <Error>e;
        }
        assert(error instanceof CancellationError);

        await this.connectRelayHost({relayHost, tunnel});
    }

    @test
    public async connectRelayHostDisposeWhileConnecting() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayHost({relayHost, tunnel, clientStreamFactory: (_) => {
                relayHost.dispose();
                throw new RelayConnectionError(
                    'error.tooManyRequests',
                    { errorType: RelayErrorType.ConnectionError, statusCode: 429 },
                );
            }});
        } catch (e) {
            error = <Error>e;
        }
        assert(error instanceof ObjectDisposedError);
    }

    @test
    public async connectRelayHostDisposeAfterConnection() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();
        const serverSession = await this.connectRelayHost({relayHost, tunnel});
        assert.strictEqual(managementClient.tunnelEndpointsUpdated, 1);
        assert.strictEqual(managementClient.tunnelEndpointsDeleted, 0);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);

        const disposed = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        await relayHost.dispose();
        assert.strictEqual(managementClient.tunnelEndpointsUpdated, 1);
        assert.strictEqual(managementClient.tunnelEndpointsDeleted, 1);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert(!relayHost.disconnectError);
        assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.byApplication);
        assert(!await disposed);

        await withTimeout(serverSession.waitUntilClosed(), 5000);
    }

    @test
    public async connectRelayHostAfterDispose() {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        const tunnel = this.createRelayTunnel();

        relayHost.dispose();
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.byApplication);
        assert(!relayHost.disconnectError);
        let error: Error | undefined = undefined;
        try {
            await this.connectRelayHost({relayHost, tunnel});
        } catch (e) {
            error = <Error>e;
        }
        assert(error instanceof ObjectDisposedError);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.byApplication);
        assert(!relayHost.disconnectError);
    }

    @test
    @params({ enableRetry: true, error: tooManyRequestsError})
    @params({ enableRetry: false, error: tooManyRequestsError})
    @params({ enableRetry: true, error: badGatewayError })
    @params({ enableRetry: false, error: badGatewayError })    
    @params({ enableRetry: true, error: serviceUnavailableError })
    @params({ enableRetry: false, error: serviceUnavailableError })    
    @params.naming((params) => `connectRelayHostRetriesOnErrorStatusCode(enableRetry: ${params.enableRetry}, statusCode: ${params.error.errorContext.statusCode})`)
    public async connectRelayHostRetriesOnErrorStatusCode(connectionOptions: TunnelConnectionOptions & {error: RelayConnectionError}) {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.strictEqual(relayHost.disconnectError, undefined);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.None);

        let isRetryAttempted = false;
        relayHost.retryingTunnelConnection((e) => {
            assert.equal(e.delayMs, maxReconnectDelayMs/2);
            assert(e.error instanceof RelayConnectionError);
            e.delayMs = 100;
            isRetryAttempted = true;
        });

        let tunnel = this.createRelayTunnel();

        let firstAttempt = true;

        const connected = connectionStatusChanged(relayHost, ConnectionStatus.Connected);
        const disconnected = connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);

        let error: Error | undefined = undefined;
        try {
            await this.connectRelayHost({relayHost, tunnel, connectionOptions, clientStreamFactory: async (stream) => {
                if (firstAttempt) {
                    firstAttempt = false;
                    throw connectionOptions.error;
                }

                return { stream, protocol: TunnelRelayTunnelHost.webSocketSubProtocol };
            }});
        } catch (e) {
            error = <Error>e;
        }

        if (connectionOptions.enableRetry) {
            assert(isRetryAttempted);
            assert(!error);
            assert.strictEqual(await connected, undefined);
            assert.strictEqual(relayHost.disconnectError, undefined);
            assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Connected);
            assert.strictEqual(relayHost.disconnectReason, undefined);

            await relayHost.dispose();
            assert.strictEqual(await disconnected, undefined);
            assert.strictEqual(relayHost.disconnectError, undefined);
            assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.byApplication);
            assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
        } else {
            assert(!isRetryAttempted);
            assert(error?.message?.includes(connectionOptions.error.message));
            assert.strictEqual(await disconnected, connectionOptions.error);
            assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
            assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.serviceNotAvailable);

            await relayHost.dispose();
            assert.strictEqual(relayHost.disconnectError, connectionOptions.error);
            assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
            assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.serviceNotAvailable);
        }
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

        const disconnectError = connectionStatusChanged(
            relayHost,
            ConnectionStatus.Disconnected,
        );

        try {
            await this.connectRelayHost({relayHost, tunnel, clientStreamFactory: () => {
                throw error;
            }});
        } catch (e) {
            assert.strictEqual((e as Error).message, expectedErrorMessage ?? error.message);
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
        let multiChannelStream = await this.connectRelayHost({relayHost, tunnel});
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
        let multiChannelStream = await this.connectRelayHost({relayHost, tunnel});
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
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        const relayHost = new TunnelRelayTunnelHost(managementClient);

        // Initialize the tunnel with a single port, then host it and connect a client.
        const testPort1 = 9985;
        const tunnel = this.createRelayTunnel([testPort1]);
        await managementClient.createTunnel(tunnel);
        const multiChannelStream = await this.connectRelayHost({relayHost, tunnel});
        const clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        const clientSshSession = this.createSshClientSession();

        try
        {
            await clientSshSession.connect(new NodeStream(clientRelayStream));

            // Try to refresh ports after connecting, before the client is authenticated.
            // It should do nothing even though the tunnel has one port.
            await relayHost.refreshPorts();
            assert.strictEqual(relayHost.remoteForwarders.size, 0);

            const clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
            await clientSshSession.authenticate(clientCredentials);

            // The one port should be forwarded (asynchronously) after authentication.
            await until(() => relayHost.remoteForwarders.size === 1, 5000);
            assert.strictEqual(relayHost.remoteForwarders.size, 1);
            assert.strictEqual([...relayHost.remoteForwarders.values()][0].localPort, testPort1);

            // Add another port to the tunnel and check that it gets forwarded.
            const testPort2 = 9986;
            await managementClient.createTunnelPort(tunnel, { portNumber: testPort2 });
            await relayHost.refreshPorts();

            assert.strictEqual(tunnel.ports!.length, 2);

            const forwardedPorts = [...relayHost.remoteForwarders.values()].map((p) => p.localPort).sort();
            assert.strictEqual(forwardedPorts.length, 2);
            assert.strictEqual(forwardedPorts[0], testPort1);
            assert.strictEqual(forwardedPorts[1], testPort2);
        }
        finally
        {
            clientRelayStream.destroy();
            clientSshSession.dispose();
            await relayHost.dispose();
        }
    }

    @test
    public async connectRelayHostRemovePort() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let tunnel = this.createRelayTunnel([9986]);
        await managementClient.createTunnel(tunnel);
        let multiChannelStream = await this.connectRelayHost({relayHost, tunnel});
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        clientSshSession.activateService(PortForwardingService)
            .acceptLocalConnectionsForForwardedPorts = false;
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

        const clientConnecting = connectionStatusChanged(relayClient, ConnectionStatus.Connecting);
        const hostConnecting = connectionStatusChanged(relayHost, ConnectionStatus.Connecting);
        clientMultiChannelStream.dropConnection();

        assert(!await clientConnecting);
        assert.strictEqual((<SshConnectionError>relayClient.disconnectError).reason, SshDisconnectReason.connectionLost);
        assert.strictEqual(relayClient.disconnectReason, SshDisconnectReason.connectionLost);

        assert(!await hostConnecting);
        assert.strictEqual((<SshConnectionError>relayHost.disconnectError).reason, SshDisconnectReason.connectionLost);
        assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.connectionLost);

        const clientConnected = connectionStatusChanged(relayClient, ConnectionStatus.Connected);
        const hostConnected = connectionStatusChanged(relayHost, ConnectionStatus.Connected);

        const [serverStream, clientStream] = await DuplexStream.createStreams();
        let newMultiChannelStream = new TestMultiChannelStream(serverStream);
        let serverConnectPromise = newMultiChannelStream.connect();
        reconnectedHostStream.resolve(clientStream);
        await serverConnectPromise;

        reconnectedClientMultiChannelStream.resolve(newMultiChannelStream);

        assert(!await clientConnected);
        assert(!relayClient.disconnectError);
        assert(!relayClient.disconnectReason);

        assert(!await hostConnected);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);

        // Add port to the tunnel host and wait for it on the client
        await testConnection.addPortOnHostAndValidateOnClient(9995);

        // Clean up
        await testConnection.dispose();
    }

    @test
    async connectRelayClientToHostAndReconnectClient() {
        const testConnection = await this.startHostWithClientAndAddPort();
        const { relayHost, relayClient, clientMultiChannelStream, clientStream } = testConnection;

        let isClientDisconnected = false;
        const relayClientReconnected = connectionStatusChanged(relayClient, ConnectionStatus.Connecting, ConnectionStatus.Connected);
        relayClient.connectionStatusChanged((e) => {
            switch (e.status) {
                case ConnectionStatus.Connecting:
                    assert(!e.disconnectError);
                    assert.strictEqual((<SshConnectionError>relayClient.disconnectError).reason, SshDisconnectReason.connectionLost);
                    assert.strictEqual(relayClient.disconnectReason, SshDisconnectReason.connectionLost);
                    assert(!relayHost.disconnectError);
                    assert(!relayHost.disconnectReason);
                    break;

                case ConnectionStatus.Connected:
                    assert(!e.disconnectError);
                    assert(!relayClient.disconnectError);
                    assert(!relayClient.disconnectReason);
                    assert(!relayHost.disconnectError);
                    assert(!relayHost.disconnectReason);
                    break;
                }
        });

        // Disconnect the tunnel client. It'll eventually reconnect. The host stays connected.
        clientStream?.channel.dispose();
        await relayClientReconnected;

        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected);
        assert(!relayClient.disconnectError);
        assert(!relayClient.disconnectReason);

        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);

        // Add port to the tunnel host and wait for it on the client
        await testConnection.addPortOnHostAndValidateOnClient(9995);
        assert.strictEqual(clientMultiChannelStream.streamsOpened, 2);

        // Clean up
        await testConnection.dispose();
    }

    @test
    async connectRelayClientToHostAndFailToReconnectClient() {
        const testConnection = await this.startHostWithClientAndAddPort();
        const { relayClient, clientStream } = testConnection;

        // Wait for client disconnection and closed SSH session
        const disconnected = connectionStatusChanged(
            relayClient,
            ConnectionStatus.Connecting,
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
        assert.strictEqual(await disconnected, error);

        await testConnection.dispose(SshDisconnectReason.connectionLost);
    }

    private async startHostWithClientAndAddPort(): Promise<TestConnection> {
        const managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        managementClient.clientRelayUri = this.mockClientRelayUri;

        // Create and start tunnel host.
        const tunnel = this.createRelayTunnel([], true); // Hosting a tunnel adds the endpoint
        await managementClient.createTunnel(tunnel);
        const relayHost = new TunnelRelayTunnelHost(managementClient);
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.None);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);

        let clientMultiChannelStream = await this.connectRelayHost({relayHost, tunnel});
        assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Connected);
        assert(!relayHost.disconnectError);
        assert(!relayHost.disconnectReason);
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

        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.None);
        assert(!relayClient.disconnectError);
        assert(!relayClient.disconnectReason);

        await relayClient.connect(tunnel);
        assert.strictEqual(clientMultiChannelStream.streamsOpened, 1);
        assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Connected);
        assert(!relayClient.disconnectError);
        assert(!relayClient.disconnectReason);

        const result: TestConnection = {
            relayHost,
            relayClient,
            managementClient,
            clientMultiChannelStream,
            clientStream,
            dispose: async (clientDisconnectReason?: SshDisconnectReason) => {
                const clientDisposed = relayClient.connectionStatus === ConnectionStatus.Disconnected ?
                    Promise.resolve(undefined) :
                    connectionStatusChanged(relayClient, ConnectionStatus.Disconnected);

                const hostDisposed = relayHost.connectionStatus === ConnectionStatus.Disconnected ?
                    Promise.resolve(undefined) :
                    connectionStatusChanged(relayHost, ConnectionStatus.Disconnected);
        
                await relayClient.dispose();
                await relayHost.dispose();

                assert(!await clientDisposed);
                assert.strictEqual(relayClient.connectionStatus, ConnectionStatus.Disconnected);
                assert.strictEqual(relayClient.disconnectReason, clientDisconnectReason || SshDisconnectReason.byApplication);
                if (clientDisconnectReason) {
                    assert.strictEqual((<SshConnectionError>relayClient.disconnectError).reason, clientDisconnectReason);
                } else {
                    assert(!relayClient.disconnectError);
                }
        
                assert(! await hostDisposed);
                assert.strictEqual(relayHost.connectionStatus, ConnectionStatus.Disconnected);
                assert.strictEqual(relayHost.disconnectReason, SshDisconnectReason.byApplication);
                assert(!relayHost.disconnectError);
            },
            addPortOnHostAndValidateOnClient: async (portNumber: number) => {
                const disposables: Disposable[] = [];
                let clientPortAdded = new Promise((resolve, reject) => {
                    relayClient.forwardedPorts?.onPortAdded((e) => resolve(e.port.remotePort), disposables);
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

    @test
    async connectRelayClientAndCancelPort()
    {
        const tunnel = this.createRelayTunnel([2000, 3000]);
        const relayClient = new TestTunnelRelayTunnelClient();
        try {
            const serverSshSession = await this.connectRelayClient({relayClient, tunnel});

            relayClient.portForwarding((e) => {
                // Cancel forwarding of port 2000. (Allow forwarding of port 3000.)
                e.cancel = e.portNumber === 2000;
            });

            const pfs = serverSshSession.activateService(PortForwardingService);
            let forwarder = await pfs!.forwardFromRemotePort('127.0.0.1', 2000);
            assert(!forwarder); // Forarding of port 2000 should have been cancelled by the client.

            forwarder = await pfs!.forwardFromRemotePort('127.0.0.1', 3000);
            assert(forwarder); // Forarding of port 3000 should NOT have been cancelled by the client.
        } finally {
            relayClient.dispose();
        }
    }
}
