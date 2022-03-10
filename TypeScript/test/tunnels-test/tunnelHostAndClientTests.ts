//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//

import * as assert from 'assert';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { MockTunnelManagementClient } from './mocks/mockTunnelManagementClient';
import { PortForwardingService } from '@vs/vs-ssh-tcp';
import {
    Tunnel,
    TunnelPort,
    TunnelConnectionMode,
    TunnelAccessScopes,
    TunnelRelayTunnelEndpoint,
} from '@vs/tunnels-contracts';
import { TunnelRelayTunnelClient, TunnelRelayTunnelHost } from '@vs/tunnels-connections';
import {
    MultiChannelStream,
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
} from '@vs/vs-ssh';
import { DuplexStream } from './duplexStream';
import * as net from 'net';
import { MockTunnelRelayStreamFactory } from './mocks/mockTunnelRelayStreamFactory';

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

    private createRelayTunnel(ports?: number[]): Tunnel {
        return {
            tunnelId: 'test',
            clusterId: 'localhost',
            accessTokens: {
                [TunnelAccessScopes.host]: 'mock-host-token',
                [TunnelAccessScopes.connect]: 'mock-connect-token',
            },
            endpoints: [
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

    private async createSshServerSession(): Promise<SshServerSession> {
        const [serverStream, clientStream] = await DuplexStream.createStreams();
        const serverSshKey = await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();

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
        relayClient: TunnelRelayTunnelClient,
        tunnel: Tunnel,
    ): Promise<SshServerSession> {
        const [serverStream, clientStream] = await DuplexStream.createStreams();
        const serverSshKey = await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();

        let sshSession = await this.createSshServerSession();
        let serverConnectPromise = sshSession.connect(serverStream);

        relayClient.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelClient.webSocketSubProtocol,
            clientStream,
        );
        await relayClient.connect(tunnel, undefined);

        await serverConnectPromise;

        return sshSession;
    }

    private async startRelayHost(
        relayHost: TunnelRelayTunnelHost,
        tunnel: Tunnel,
    ): Promise<MultiChannelStream> {
        const [serverStream, clientStream] = await DuplexStream.createStreams();

        let multiChannelStream = new MultiChannelStream(serverStream);
        let serverConnectPromise = multiChannelStream.connect();

        relayHost.streamFactory = new MockTunnelRelayStreamFactory(
            TunnelRelayTunnelHost.webSocketSubProtocol,
            clientStream,
        );
        await relayHost.start(tunnel);

        await serverConnectPromise;

        return multiChannelStream;
    }

    @test
    public async connectRelayClientTest() {
        let relayClient = new TunnelRelayTunnelClient();

        relayClient.connectionModes.forEach((connectionMode) => {
            assert.strictEqual(connectionMode, TunnelConnectionMode.TunnelRelay);
        });

        let tunnel = this.createRelayTunnel();
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
    }

    @test
    public async connectRelayClientAddPort() {
        let relayClient = new TunnelRelayTunnelClient();

        let tunnel = this.createRelayTunnel();
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9881;
        let remotePortStreamer = await pfs.streamFromRemotePort('::', testPort);
        assert.notStrictEqual(remotePortStreamer, null);
        assert.strictEqual(testPort, remotePortStreamer!.remotePort);

        let tcs = new PromiseCompletionSource<any>();
        remotePortStreamer?.onStreamOpened(async (stream: SshStream) => {
            const nodeStream = new NodeStream(stream);
            nodeStream.close();
            await tcs.promise;
        });

        const socket = new net.Socket();
        socket.connect(testPort, '127.0.0.1', async () => {});
        socket.destroy();
        remotePortStreamer?.dispose();
    }

    @test
    public async connectRelayClientAddPortInUse() {
        let relayClient = new TunnelRelayTunnelClient();

        let tunnel = this.createRelayTunnel([9982]);
        let serverSshSession = await this.connectRelayClient(relayClient, tunnel);
        let pfs = serverSshSession.activateService(PortForwardingService);

        let testPort = 9982;
        const socket = new net.Socket();
        socket.connect(testPort, '127.0.0.1', async () => {
            let remotePortStreamer = await pfs.streamFromRemotePort('127.0.0.1', testPort);
            assert.notStrictEqual(remotePortStreamer, null);
            assert.notStrictEqual(testPort, remotePortStreamer?.remotePort);

            // The next available port number should have been selected.
            assert.strictEqual(testPort + 1, remotePortStreamer?.remotePort);
        });
        socket.destroy();
    }

    @test
    public async connectRelayClientRemovePort() {
        let relayClient = new TunnelRelayTunnelClient();

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

        let tunnel = this.createRelayTunnel();
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);

        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );

        let clientSshSession = this.createSshClientSession();
        let pfs = clientSshSession.activateService(PortForwardingService);
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        clientRelayStream.destroy();
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
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);

        while (Object.keys(relayHost.remoteForwarders).length < 1) {
            await new Promise((r) => setTimeout(r, 2000));
        }

        assert.strictEqual(tunnel.ports!.length, 1);
        const forwardedPort = tunnel.ports![0];

        let forwarder = relayHost.remoteForwarders[9984];
        if (forwarder) {
            assert.strictEqual(forwardedPort.portNumber, forwarder.localPort);
            assert.strictEqual(forwardedPort.portNumber, forwarder.remotePort);
        }
        clientRelayStream.destroy();
        clientSshSession.dispose();
    }

    @test
    public async connectRelayHostAddPort() {
        let managementClient = new MockTunnelManagementClient();
        managementClient.hostRelayUri = this.mockHostRelayUri;
        let relayHost = new TunnelRelayTunnelHost(managementClient);

        let tunnel = this.createRelayTunnel();
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);

        await relayHost.addPort({ portNumber: 9985 });

        assert.strictEqual(tunnel.ports!.length, 1);
        const forwardedPort = tunnel.ports![0];

        let forwarder = relayHost.remoteForwarders[9985];
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
        let multiChannelStream = await this.startRelayHost(relayHost, tunnel);
        let clientRelayStream = await multiChannelStream.openStream(
            TunnelRelayTunnelHost.clientStreamChannelType,
        );
        let clientSshSession = this.createSshClientSession();
        await clientSshSession.connect(new NodeStream(clientRelayStream));
        let clientCredentials: SshClientCredentials = { username: 'tunnel', password: undefined };
        await clientSshSession.authenticate(clientCredentials);

        while (Object.keys(relayHost.remoteForwarders).length < 1) {
            await new Promise((r) => setTimeout(r, 2000));
        }
        await relayHost.removePort(9986);

        assert.strictEqual(tunnel.ports!.length, 0);

        assert.notStrictEqual(relayHost.remoteForwarders, {});
        clientSshSession.dispose();
    }
}
