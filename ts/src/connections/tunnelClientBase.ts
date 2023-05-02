// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Duplex } from 'stream';
import {
    Tunnel,
    TunnelAccessScopes,
    TunnelConnectionMode,
    TunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import {
    CancellationToken,
    SecureStream,
    SessionRequestMessage,
    SshAuthenticatingEventArgs,
    SshClientCredentials,
    SshClientSession,
    SshDisconnectReason,
    SshRequestEventArgs,
    SshSessionClosedEventArgs,
    SshStream,
    Stream,
    Trace,
} from '@microsoft/dev-tunnels-ssh';
import { ForwardedPortConnectingEventArgs, ForwardedPortsCollection, PortForwardingService } from '@microsoft/dev-tunnels-ssh-tcp';
import { RetryTcpListenerFactory } from './retryTcpListenerFactory';
import { isNode, SshHelpers } from './sshHelpers';
import { TunnelClient } from './tunnelClient';
import { getError, List } from './utils';
import { Emitter } from 'vscode-jsonrpc';
import { TunnelConnectionSession } from './tunnelConnectionSession';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { tunnelSshSessionClass } from './tunnelSshSessionClass';
import { PortRelayConnectResponseMessage } from './messages/portRelayConnectResponseMessage';

export const webSocketSubProtocol = 'tunnel-relay-client';
export const webSocketSubProtocolv2 = 'tunnel-relay-client-v2-dev';

/**
 * Base class for clients that connect to a single host
 */
export class TunnelClientBase
    extends tunnelSshSessionClass<SshClientSession>(TunnelConnectionSession)
    implements TunnelClient {
    private readonly sshSessionClosedEmitter = new Emitter<this>();
    private acceptLocalConnectionsForForwardedPortsValue: boolean = isNode();
    private localForwardingHostAddressValue: string = '127.0.0.1';
    private connectionProtocolValue?: string;

    public connectionModes: TunnelConnectionMode[] = [];

    /**
     * Tunnel endpoints this client connects to.
     * Depending on implementation, the client may connect to one or more endpoints.
     */
    public endpoints?: TunnelEndpoint[];

    protected get isSshSessionActive(): boolean {
        return !!this.sshSession?.isConnected;
    }

    protected readonly sshSessionClosed = this.sshSessionClosedEmitter.event;

    /**
     * Get a value indicating if remote port is forwarded and has any channels open on the client,
     * whether used by local tcp listener if {AcceptLocalConnectionsForForwardedPorts} is true, or
     * streamed via <see cref="ConnectToForwardedPortAsync(int, CancellationToken)"/>.
     */
    protected hasForwardedChannels(port: number): boolean {
        if (!this.isSshSessionActive) {
            return false;
        }

        const pfs = this.sshSession?.activateService(PortForwardingService);
        const remoteForwardedPorts = pfs?.remoteForwardedPorts;
        const forwardedPort = remoteForwardedPorts?.find((p) => p.remotePort === port);
        return !!forwardedPort && remoteForwardedPorts!.getChannels(forwardedPort).length > 0;
    }

    /**
     * A value indicating whether local connections for forwarded ports are accepted.
     * Local connections are not accepted if the host is not NodeJS (e.g. browser).
     */
    public get acceptLocalConnectionsForForwardedPorts(): boolean {
        return this.acceptLocalConnectionsForForwardedPortsValue;
    }

    public set acceptLocalConnectionsForForwardedPorts(value: boolean) {
        if (value === this.acceptLocalConnectionsForForwardedPortsValue) {
            return;
        }

        if (value && !isNode()) {
            throw new Error(
                'Cannot accept local connections for forwarded ports on this platform.',
            );
        }

        this.acceptLocalConnectionsForForwardedPortsValue = value;
        this.configurePortForwardingService();
    }

    /**
     * Gets the local network interface address that the tunnel client listens on when
     * accepting connections for forwarded ports.
     */
    public get localForwardingHostAddress(): string {
        return this.localForwardingHostAddressValue;
    }

    public set localForwardingHostAddress(value: string) {
        if (value !== this.localForwardingHostAddressValue) {
            this.localForwardingHostAddressValue = value;
            this.configurePortForwardingService();
        }
    }

    public get forwardedPorts(): ForwardedPortsCollection | undefined {
        const pfs = this.sshSession?.activateService(PortForwardingService);
        return pfs?.remoteForwardedPorts;
    }

    public constructor(trace?: Trace, managementClient?: TunnelManagementClient) {
        super(TunnelAccessScopes.Connect, trace, managementClient);
    }

    /**
     * Connection protocol used to connect to the host.
     */
    public get connectionProtocol(): string | undefined {
        return this.connectionProtocolValue;
    }
    protected set connectionProtocol(value: string | undefined) {
        this.connectionProtocolValue = value;
    }

    public async connectClient(tunnel: Tunnel, endpoints: TunnelEndpoint[]): Promise<void> {
        this.endpoints = endpoints;
        await this.connectTunnelSession(tunnel);
    }

    public async connect(tunnel: Tunnel, hostId?: string): Promise<void> {
        const endpoints = TunnelClientBase.getEndpoints(tunnel, hostId);
        await this.connectClient(tunnel, endpoints);
    }

    /**
     * Validate the tunnel and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     * @param tunnel Tunnel to use for the connection.
     *     Tunnel object to get the connection data if defined.
     *     Undefined if the connection data is already known.
     * @internal
     */
    public async onConnectingToTunnel(tunnel?: Tunnel): Promise<void> {
        if (!this.endpoints) {
            this.endpoints = TunnelClientBase.getEndpoints(tunnel);
        }
        await super.onConnectingToTunnel(tunnel);
    }

    private static getEndpoints(tunnel?: Tunnel, hostId?: string): TunnelEndpoint[] {
        if (!tunnel) {
            throw new Error('Tunnel must be defined.');
        }
        if (!tunnel.endpoints) {
            throw new Error('Tunnel endpoints cannot be null');
        }
        if (tunnel.endpoints.length === 0) {
            throw new Error('No hosts are currently accepting connections for the tunnel.');
        }

        const endpointGroups = List.groupBy(
            tunnel.endpoints,
            (endpoint: TunnelEndpoint) => endpoint.hostId,
        );

        let endpoints: TunnelEndpoint[];
        if (hostId) {
            endpoints = endpointGroups.get(hostId)!;
            if (!endpoints) {
                throw new Error(
                    'The specified host is not currently accepting connections to the tunnel.',
                );
            }
        } else if (endpointGroups.size > 1) {
            throw new Error(
                'There are multiple hosts for the tunnel. Specify a host ID to connect to.',
            );
        } else {
            endpoints = endpointGroups.entries().next().value[1];
        }

        return endpoints;
    }

    private onRequest(e: SshRequestEventArgs<SessionRequestMessage>) {
        if (
            e.request.requestType === PortForwardingService.portForwardRequestType ||
            e.request.requestType === PortForwardingService.cancelPortForwardRequestType
        ) {
            e.isAuthorized = true;
        }
    }

    public startSshSession(stream: Stream, cancellation?: CancellationToken): Promise<void> {
        return this.connectSession(async () => {
            this.sshSession = SshHelpers.createSshClientSession((config) => {
                // Enable port-forwarding via the SSH protocol.
                config.addService(PortForwardingService);
            });
            this.sshSession.trace = this.trace;
            this.sshSession.onClosed((e) => this.onSshSessionClosed(e));
            this.sshSession.onAuthenticating((e) => this.onSshServerAuthenticating(e));
            this.sshSession.onDisconnected((e) => this.onSshSessionDisconnected());

            try {
                this.configurePortForwardingService();

                this.sshSession.onRequest((e) => this.onRequest(e));

                await this.sshSession.connect(stream, cancellation);

                // For now, the client is allowed to skip SSH authentication;
                // they must have a valid tunnel access token already to get this far.
                const clientCredentials: SshClientCredentials = {
                    username: 'tunnel',
                    password: undefined,
                };

                await this.sshSession.authenticate(clientCredentials, cancellation);
            } catch (e) {
                const error = getError(e, 'Error starting tunnel client SSH session: ');
                await this.closeSession(error);
                throw error;
            }
        });
    }

    private configurePortForwardingService() {
        if (!this.sshSession) {
            return;
        }

        const pfs = this.sshSession.activateService(PortForwardingService);
        // Do not start forwarding local connections for browser client connections or if this is not allowed.
        if (this.acceptLocalConnectionsForForwardedPortsValue && isNode()) {
            pfs.tcpListenerFactory = new RetryTcpListenerFactory(
                this.localForwardingHostAddressValue,
            );
        } else {
            pfs.acceptLocalConnectionsForForwardedPorts = false;
        }

        if (this.connectionProtocol === webSocketSubProtocolv2) {
            pfs.messageFactory = this;
            pfs.onForwardedPortConnecting((e) => this.onForwardedPortConnecting(e));
        }
    }

    /**
     * Invoked when a forwarded port is connecting. (Only for V2 protocol.)
     */
    protected onForwardedPortConnecting(e: ForwardedPortConnectingEventArgs) {
        // With V2 protocol, the relay server always sends an extended response message
        // with a property indicating whether E2E encryption is enabled for the connection.
        const channel = e.stream.channel;
        const relayResponseMessage = channel.openConfirmationMessage
            .convertTo(new PortRelayConnectResponseMessage());

        if (relayResponseMessage.isE2EEncryptionEnabled) {
            // The host trusts the relay to authenticate the client, so it doesn't require
            // any additional password/token for client authentication.
            const clientCredentials: SshClientCredentials = { username: "tunnel" };

            e.transformPromise = new Promise((resolve, reject) => {
                const secureStream = new SecureStream(
                    e.stream,
                    clientCredentials);
                secureStream.trace = this.trace;

                // TODO: Verify the host public key shared via the tunnel service?
                secureStream.onAuthenticating((authEvent) =>
                    authEvent.authenticationPromise = Promise.resolve({}));

                // Do not pass the cancellation token from the connecting event,
                // because the connection will outlive the event.
                secureStream.connect().then(() => resolve(secureStream)).catch(reject);
            });
        }

        super.onForwardedPortConnecting(e);
    }

    public async connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<Duplex> {
        const pfs = this.getSshSessionPfs();
        if (!pfs) {
            throw new Error(
                'Failed to connect to remote port. Ensure that the client has connected by calling connectClient.',
            );
        }
        return pfs.connectToForwardedPort(fowardedPort, cancellation);
    }

    public async waitForForwardedPort(
        forwardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<void> {
        const pfs = this.getSshSessionPfs();
        if (!pfs) {
            throw new Error(
                'Port forwarding has not been started. Ensure that the client has connected by calling connectClient.',
            );
        }
        return pfs.waitForForwardedPort(forwardedPort, cancellation);
    }

    private getSshSessionPfs() {
        return this.sshSession?.getService(PortForwardingService) ?? undefined;
    }

    private onSshServerAuthenticating(e: SshAuthenticatingEventArgs): void {
        // TODO: Validate host public keys match those published to the service?
        // For now, the assumption is only a host with access to the tunnel can get a token
        // that enables listening for tunnel connections.
        e.authenticationPromise = Promise.resolve({});
    }

    public async refreshPorts(): Promise<void> {
        if (!this.sshSession || this.sshSession.isClosed) {
            throw new Error('Not connected.');
        }

        const request = new SessionRequestMessage();
        request.requestType = 'RefreshPorts';
        request.wantReply = true;
        await this.sshSession.request(request);
    }

    private onSshSessionClosed(e: SshSessionClosedEventArgs) {
        this.sshSessionClosedEmitter.fire(this);
        if (e.reason === SshDisconnectReason.connectionLost) {
            this.startReconnectingIfNotDisposed();
        }
    }

    private onSshSessionDisconnected() {
        this.startReconnectingIfNotDisposed();
    }
}
