// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Duplex } from 'stream';
import {
    Tunnel,
    TunnelAccessScopes,
    TunnelConnectionMode,
    TunnelEndpoint,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import {
    CancellationToken,
    SecureStream,
    SessionRequestMessage,
    SshAlgorithms,
    SshAuthenticatingEventArgs,
    SshAuthenticationType,
    SshClientCredentials,
    SshConnectionError,
    SshDisconnectReason,
    SshProtocolExtensionNames,
    SshRequestEventArgs,
    SshSessionClosedEventArgs,
    Stream,
    Trace,
    TraceLevel,
} from '@microsoft/dev-tunnels-ssh';
import { ForwardedPortConnectingEventArgs, ForwardedPortEventArgs, ForwardedPortsCollection, PortForwardRequestMessage, PortForwardingService } from '@microsoft/dev-tunnels-ssh-tcp';
import { RetryTcpListenerFactory } from './retryTcpListenerFactory';
import { isNode, SshHelpers } from './sshHelpers';
import { TunnelClient } from './tunnelClient';
import { List } from './utils';
import { Emitter } from 'vscode-jsonrpc';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { PortRelayConnectResponseMessage } from './messages/portRelayConnectResponseMessage';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import { TunnelConnectionSession } from './tunnelConnectionSession';
import { PortForwardingEventArgs } from './portForwardingEventArgs';

export const webSocketSubProtocol = 'tunnel-relay-client';
export const webSocketSubProtocolv2 = 'tunnel-relay-client-v2-dev';

// Check for an environment variable to determine which protocol version to use.
// By default, prefer V2 and fall back to V1.
const protocolVersion = process?.env && process.env.DEVTUNNELS_PROTOCOL_VERSION;
const connectionProtocols =
    protocolVersion === '1' ? [webSocketSubProtocol] :
    protocolVersion === '2' ? [webSocketSubProtocolv2] :
    [webSocketSubProtocolv2, webSocketSubProtocol];

/**
 * Tunnel client implementation that connects via a tunnel relay.
 */
export class TunnelRelayTunnelClient extends TunnelConnectionSession implements TunnelClient {
    public static readonly webSocketSubProtocol = webSocketSubProtocol;
    public static readonly webSocketSubProtocolv2 = webSocketSubProtocolv2;

    public constructor(managementClient?: TunnelManagementClient, trace?: Trace) {
        super(TunnelAccessScopes.Connect, connectionProtocols, managementClient, trace);
    }

    private readonly portForwardingEmitter = new Emitter<PortForwardingEventArgs>();
    private readonly sshSessionClosedEmitter = new Emitter<this>();
    private acceptLocalConnectionsForForwardedPortsValue: boolean = isNode();
    private localForwardingHostAddressValue: string = '127.0.0.1';
    private hostId?: string;
    private readonly disconnectedStreams = new Map<number, SecureStream[]>();

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

    /**
     * Event raised when a port is about to be forwarded to the client.
     *
     * The application may cancel this event to prevent specific port(s) from being
     * forwarded to the client. Cancelling prevents the tunnel client from listening on
     * a local socket for the port, AND prevents use of {@link connectToForwardedPort}
     * to open a direct stream connection to the port.
     */
    public readonly portForwarding = this.portForwardingEmitter.event;

    /**
     * Extensibility point and unit test hook.
     * This event fires when the client SSH session is disconnected or closed either by this client or Relay.
     */
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

    public async connect(
        tunnel: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<void> {
        this.hostId = options?.hostId;
        await this.connectTunnelSession(tunnel, options, cancellation);
    }

    protected tunnelChanged() {
        super.tunnelChanged();
        this.endpoints = undefined;
        if (this.tunnel) {
            if (!this.tunnel.endpoints) {
                throw new Error('Tunnel endpoints cannot be null');
            }

            if (this.tunnel.endpoints.length === 0) {
                throw new Error('No hosts are currently accepting connections for the tunnel.');
            }

            const endpointGroups = List.groupBy(this.tunnel.endpoints, (ep) => ep.hostId);

            if (this.hostId) {
                this.endpoints = endpointGroups.get(this.hostId)!;
                if (!this.endpoints) {
                    throw new Error(
                        'The specified host is not currently accepting connections to the tunnel.',
                    );
                }
            } else if (endpointGroups.size > 1) {
                throw new Error(
                    'There are multiple hosts for the tunnel. Specify a host ID to connect to.',
                );
            } else {
                this.endpoints = endpointGroups.entries().next().value?.[1];
            }

            const tunnelEndpoints: TunnelRelayTunnelEndpoint[] = this.endpoints!.filter(
                (ep) => ep.connectionMode === TunnelConnectionMode.TunnelRelay,
            );

            if (tunnelEndpoints.length === 0) {
                throw new Error('The host is not currently accepting Tunnel relay connections.');
            }

            // TODO: What if there are multiple relay endpoints, which one should the tunnel client pick, or is this an error?
            // For now, just chose the first one.
            const endpoint = tunnelEndpoints[0];
            this.hostPublicKeys = endpoint.hostPublicKeys;
            this.relayUri = endpoint.clientRelayUri!;
        } else {
            this.relayUri = undefined;
        }
    }

    private onRequest(e: SshRequestEventArgs<SessionRequestMessage>) {
        if (e.request.requestType === PortForwardingService.portForwardRequestType) {
            // The event-handler has a chance to cancel forwarding of this port.
            const request = <PortForwardRequestMessage>e.request;
            const args = new PortForwardingEventArgs(request.port);
            this.portForwardingEmitter.fire(args);
            e.isAuthorized = !args.cancel;
        } else if (e.request.requestType === PortForwardingService.cancelPortForwardRequestType) {
            e.isAuthorized = true;
        }
    }

    /**
     * Configures the tunnel session with the given stream.
     * @internal
     */
    public async configureSession(
        stream: Stream,
        protocol: string,
        isReconnect: boolean,
        cancellation: CancellationToken,
    ): Promise<void> {
        this.connectionProtocol = protocol;
        if (isReconnect && this.sshSession && !this.sshSession.isClosed) {
            await this.sshSession.reconnect(stream, cancellation);
        } else {
            await this.startSshSession(stream, cancellation);
        }
    }    

    public startSshSession(stream: Stream, cancellation?: CancellationToken): Promise<void> {
        return this.connectSession(async () => {
            this.sshSession = SshHelpers.createSshClientSession((config) => {
                // Enable port-forwarding via the SSH protocol.
                config.addService(PortForwardingService);

                if (this.connectionProtocol === webSocketSubProtocol) {
                    // Enable client SSH session reconnect for V1 protocol only.
                    // (V2 SSH reconnect is handled by the SecureStream class.)
                    config.protocolExtensions.push(SshProtocolExtensionNames.sessionReconnect);
                } else {
                    // The V2 protocol configures optional encryption, including "none" as an enabled
                    // and preferred key-exchange algorithm, because encryption of the outer SSH
                    // session is optional since it is already over a TLS websocket.
                    config.keyExchangeAlgorithms.splice(0, 0, SshAlgorithms.keyExchange.none);
                }

                // Configure keep-alive if requested
                const keepAliveInterval = this.connectionOptions?.keepAliveIntervalInSeconds;
                if (keepAliveInterval && keepAliveInterval > 0) {
                    config.keepAliveTimeoutInSeconds = keepAliveInterval;
                }
            });
            this.sshSession.trace = this.trace;
            this.sshSession.onReportProgress(
                (args) => this.raiseReportProgress(args.progress, args.sessionNumber),
                this,
                this.sshSessionDisposables);
            this.sshSession.onClosed(this.onSshSessionClosed, this, this.sshSessionDisposables);
            this.sshSession.onAuthenticating(this.onSshServerAuthenticating, this, this.sshSessionDisposables);
            this.sshSession.onDisconnected(this.onSshSessionDisconnected, this, this.sshSessionDisposables);
            this.sshSession.onRequest(this.onRequest, this, this.sshSessionDisposables);

            this.sshSession.onKeepAliveFailed((count) => this.onKeepAliveFailed(count));
            this.sshSession.onKeepAliveSucceeded((count) => this.onKeepAliveSucceeded(count));

            const pfs = this.sshSession.activateService(PortForwardingService);
            if (this.connectionProtocol === webSocketSubProtocolv2) {
                pfs.messageFactory = this;
                pfs.onForwardedPortConnecting(this.onForwardedPortConnecting, this, this.sshSessionDisposables);
                pfs.remoteForwardedPorts.onPortAdded((e) => this.onForwardedPortAdded(pfs, e), this, this.sshSessionDisposables);
                pfs.remoteForwardedPorts.onPortUpdated((e) => this.onForwardedPortAdded(pfs, e), this, this.sshSessionDisposables);
            }

            this.configurePortForwardingService();

            await this.sshSession.connect(stream, cancellation);

            // SSH authentication is required in V1 protocol, optional in V2 depending on
            // whether the session enabled key exchange (as indicated by having a session ID
            // or not).In either case a password is not required. Strong authentication was
            // already handled by the relay service via the tunnel access token used for the
            // websocket connection.
            if (this.sshSession.sessionId) {
                // Use a snapshot of this.sshSession because if authenticate() fails, it closes the session,
                // and onSshSessionClosed() clears up this.sshSession. 
                const session = this.sshSession;
                const clientCredentials: SshClientCredentials = { username: 'tunnel' };
                if (!await session.authenticate(clientCredentials, cancellation)) {
                    throw new Error(session.principal ?
                        'SSH client authentication failed.' :
                        'SSH server authentication failed.');
                }
            }
        });
    }

    private configurePortForwardingService() {
        const pfs = this.getSshSessionPfs();
        if (!pfs) {
            return;
        }

        // Do not start forwarding local connections for browser client connections or if this is not allowed.
        if (this.acceptLocalConnectionsForForwardedPortsValue && isNode()) {
            pfs.tcpListenerFactory = new RetryTcpListenerFactory(
                this.localForwardingHostAddressValue,
            );
        } else {
            pfs.acceptLocalConnectionsForForwardedPorts = false;
        }
    }

    private onForwardedPortAdded(pfs: PortForwardingService, e: ForwardedPortEventArgs) {
        const port = e.port.remotePort;
        if (typeof port !== 'number') {
            return;
        }

        // If there are disconnected streams for the port, re-connect them now.
        const disconnectedStreamsCount = this.disconnectedStreams.get(port)?.length ?? 0;
        for (let i = 0; i < disconnectedStreamsCount; i++) {
            pfs.connectToForwardedPort(port)
            .then(() => {
                this.trace(TraceLevel.Verbose, 0, `Reconnected stream to fowarded port ${port}`);
            }).catch((error) => {
                this.trace(
                    TraceLevel.Warning,
                    0,
                    `Failed to reconnect to forwarded port ${port}: ${error}`);

                // The host is no longer accepting connections on the forwarded port?
                // Clear the list of disconnected streams for the port, because
                // it seems it is no longer possible to reconnect them.
                const streams = this.disconnectedStreams.get(port);
                if (streams) {
                    while (streams.length > 0) {
                        streams.pop()!.dispose();
                    }
                }
            });
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
                // If there's a disconnected SecureStream for the port, try to reconnect it.
                // If there are multiple, pick one and the host will match by SSH session ID.
                let secureStream = this.disconnectedStreams.get(e.port)?.shift();
                if (secureStream) {
                    this.trace(
                        TraceLevel.Verbose,
                        0,
                        `Reconnecting encrypted stream for port ${e.port}...`);
                    secureStream.reconnect(e.stream)
                        .then(() => {
                            this.trace(
                                TraceLevel.Verbose,
                                0,
                                `Reconnecting encrypted stream for port ${e.port} succeeded.`);
                            resolve(secureStream!);
                        }).catch(reject);
                } else {
                    secureStream = new SecureStream(
                        e.stream,
                        clientCredentials);
                    secureStream.trace = this.trace;
                    secureStream.onAuthenticating((authEvent) => authEvent.authenticationPromise =
                        this.onHostAuthenticating(authEvent).catch());
                    secureStream.onDisconnected(
                        () => this.onSecureStreamDisconnected(e.port, secureStream!));

                    // Do not pass the cancellation token from the connecting event,
                    // because the connection will outlive the event.
                    secureStream.connect().then(() => resolve(secureStream!)).catch(reject);
                }

            });
        }

        super.onForwardedPortConnecting(e);
    }

    private onSecureStreamDisconnected(port: number, secureStream: SecureStream): void {
        this.trace(TraceLevel.Verbose, 0, `Encrypted stream for port ${port} disconnected.`);

        const streams = this.disconnectedStreams.get(port);
        if (streams) {
            streams.push(secureStream);
        } else {
            this.disconnectedStreams.set(port, [secureStream]);
        }
    }

    private async onHostAuthenticating(e: SshAuthenticatingEventArgs): Promise<object | null> {
        if (e.authenticationType !== SshAuthenticationType.serverPublicKey || !e.publicKey) {
            this.traceWarning('Invalid host authenticating event.');
            return null;
        }

        // The public key property on this event comes from SSH key-exchange; at this point the
        // SSH server has cryptographically proven that it holds the corresponding private key.
        // Convert host key bytes to base64 to match the format in which the keys are published.
        const hostKey = (await e.publicKey.getPublicKeyBytes(e.publicKey.keyAlgorithmName))
            ?.toString('base64') ?? '';

        // Host public keys are obtained from the tunnel endpoint record published by the host.
        if (!this.hostPublicKeys) {
            this.traceWarning('Host identity could not be verified because ' +
                'no public keys were provided.');
            this.traceVerbose(`Host key: ${hostKey}`);
            return {};
        } 

        if (this.hostPublicKeys.includes(hostKey)) {
            this.traceVerbose(`Verified host identity with public key ${hostKey}`);
            return {};
        } 
        
        // The tunnel host may have reconnected with a different host public key.
        // Try fetching the tunnel again to refresh the key.
        if (!this.disposeToken.isCancellationRequested &&
            await this.refreshTunnel(false, this.disposeToken) &&
            this.hostPublicKeys.includes(hostKey)) {
            this.traceVerbose('Verified host identity with public key ' + hostKey);
            return {};
        }

        this.traceError('Host public key verification failed.');
        this.traceVerbose(`Host key: ${hostKey}`);
        this.traceVerbose(`Expected key(s): ${this.hostPublicKeys.join(', ')}`);
        return null;
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

    /**
     * @internal Closes the tunnel session due to an error.
     */
    public async closeSession(reason?: SshDisconnectReason, error?: Error): Promise<void> {
        if (this.isSshSessionActive) {
            this.sshSessionClosedEmitter.fire(this);
        }

        await super.closeSession(reason, error);
    }

    /**
     * SSH session closed event handler.
     */    
    protected onSshSessionClosed(e: SshSessionClosedEventArgs) {
        this.sshSessionClosedEmitter.fire(this);
        super.onSshSessionClosed(e);
    }

    private onSshSessionDisconnected() {
        this.sshSessionClosedEmitter.fire(this);
        const reason = SshDisconnectReason.connectionLost;
        const error = new SshConnectionError("Connection lost.", SshDisconnectReason.connectionLost);
        this.maybeStartReconnecting(reason, undefined, error);
    }

    /**
     * Connect to the tunnel session on the relay service using the given access token for authorization.
     */
    protected async connectClientToRelayServer(
        clientRelayUri: string,
        accessToken?: string,
    ): Promise<void> {
        if (!clientRelayUri) {
            throw new Error('Client relay URI must be a non-empty string');
        }

        this.relayUri = clientRelayUri;
        this.accessToken = accessToken;
        await this.connectTunnelSession();
    }
}
