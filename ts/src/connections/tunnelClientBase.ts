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
    SshAuthenticationType,
    SshClientCredentials,
    SshClientSession,
    SshDisconnectReason,
    SshRequestEventArgs,
    SshSessionClosedEventArgs,
    SshStream,
    Stream,
    Trace,
    TraceLevel,
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

    public connectionModes: TunnelConnectionMode[] = [];

    /**
     * Tunnel endpoints this client connects to.
     * Depending on implementation, the client may connect to one or more endpoints.
     */
    public endpoints?: TunnelEndpoint[];

    /**
     * One or more SSH public keys published by the host with the tunnel endpoint.
     */
    protected hostPublicKeys?: string[];

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

                // SSH authentication is required in V1 protocol, optional in V2 depending on
                // whether the session enabled key exchange (as indicated by having a session ID
                // or not).In either case a password is not required. Strong authentication was
                // already handled by the relay service via the tunnel access token used for the
                // websocket connection.
                if (this.sshSession.sessionId) {
                    const clientCredentials: SshClientCredentials = { username: 'tunnel' };
                    if (!(await this.sshSession.authenticate(clientCredentials, cancellation))) {
                        throw new Error(this.sshSession.principal ?
                            'SSH client authentication failed.' :
                            'SSH server authentication failed.');
                    }
                }
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
                secureStream.onAuthenticating((authEvent) =>
                    authEvent.authenticationPromise = this.onHostAuthenticating(authEvent).catch());

                // Do not pass the cancellation token from the connecting event,
                // because the connection will outlive the event.
                secureStream.connect().then(() => resolve(secureStream)).catch(reject);
            });
        }

        super.onForwardedPortConnecting(e);
    }

    private async onHostAuthenticating(e: SshAuthenticatingEventArgs): Promise<object | null> {
        if (e.authenticationType !== SshAuthenticationType.serverPublicKey || !e.publicKey) {
            this.trace(TraceLevel.Warning, 0, 'Invalid host authenticating event.');
            return null;
        }

        // The public key property on this event comes from SSH key-exchange; at this point the
        // SSH server has cryptographically proven that it holds the corresponding private key.
        // Convert host key bytes to base64 to match the format in which the keys are published.
        const hostKey = (await e.publicKey.getPublicKeyBytes(e.publicKey.keyAlgorithmName))
            ?.toString('base64') ?? '';

        // Host public keys are obtained from the tunnel endpoint record published by the host.
        if (!this.hostPublicKeys) {
            this.trace(TraceLevel.Warning, 0, 'Host identity could not be verified because ' +
                'no public keys were provided.');
            this.trace(TraceLevel.Verbose, 0, 'Host key: ' + hostKey);
            return {};
        } else if (this.hostPublicKeys.includes(hostKey)) {
            this.trace(TraceLevel.Verbose, 0, 'Verified host identity with public key ' + hostKey);
            return {};
        } else {
            this.trace(TraceLevel.Error, 0, "Host public key verificiation failed.");
            this.trace(TraceLevel.Verbose, 0, "Host key: " + hostKey);
            this.trace(TraceLevel.Verbose, 0, "Expected key(s): " + this.hostPublicKeys.join(', '));
            return null;
        }
    }

    private onSshServerAuthenticating(e: SshAuthenticatingEventArgs): void {
        if (this.connectionProtocol === webSocketSubProtocol) {
            // For V1 protocol the SSH server is the host; it should be authenticated with public key.
            e.authenticationPromise = this.onHostAuthenticating(e);
        } else {
            // For V2 protocol the SSH server is the relay.
            // Relay server authentication is done via the websocket TLS host certificate.
            // If SSH encryption/authentication is used anyway, just accept any SSH host key.
            e.authenticationPromise = Promise.resolve({});
        }
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
        
        this.trace(TraceLevel.Verbose, 0, 'Waiting for forwarded port ' + forwardedPort);
        await pfs.waitForForwardedPort(forwardedPort, cancellation);
        this.trace(TraceLevel.Verbose, 0, 'Forwarded port ' + forwardedPort + ' is ready.');
    }

    private getSshSessionPfs() {
        return this.sshSession?.getService(PortForwardingService) ?? undefined;
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
