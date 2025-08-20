// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelConnectionMode,
    TunnelProtocol,
    TunnelRelayTunnelEndpoint,
    TunnelPort,
    Tunnel,
    TunnelAccessScopes,
    TunnelProgress,
    TunnelEvent,
} from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import {
    SshChannelOpeningEventArgs,
    SshChannelOpenFailureReason,
    SshStream,
    SshSessionClosedEventArgs,
    SshDisconnectReason,
    KeyPair,
    TraceLevel,
    SshServerSession,
    SshAuthenticatingEventArgs,
    NodeStream,
    SshAuthenticationType,
    PromiseCompletionSource,
    CancellationError,
    Trace,
    SshChannel,
    Stream,
    SessionRequestMessage,
    SshRequestEventArgs,
    SessionRequestSuccessMessage,
    SshClientSession,
    SshSessionConfiguration,
    SshAlgorithms,
    SshSession,
    SshServerCredentials,
    SecureStream,
    SshProtocolExtensionNames,
    SshConnectionError,
} from '@microsoft/dev-tunnels-ssh';
import {
    ForwardedPortConnectingEventArgs,
    PortForwardChannelOpenMessage,
    PortForwardingService,
    RemotePortForwarder,
} from '@microsoft/dev-tunnels-ssh-tcp';
import { CancellationToken } from 'vscode-jsonrpc';
import { SshHelpers } from './sshHelpers';
import { MultiModeTunnelHost } from './multiModeTunnelHost';
import { SessionPortKey } from './sessionPortKey';
import { PortRelayConnectRequestMessage } from './messages/portRelayConnectRequestMessage';
import { PortRelayConnectResponseMessage } from './messages/portRelayConnectResponseMessage';
import { v4 as uuidv4 } from 'uuid';
import { TunnelHost } from './tunnelHost';
import { isNode } from './sshHelpers';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import { TunnelConnectionSession } from './tunnelConnectionSession';

const webSocketSubProtocol = 'tunnel-relay-host';
const webSocketSubProtocolv2 = 'tunnel-relay-host-v2-dev';

// Check for an environment variable to determine which protocol version to use.
// By default, prefer V2 and fall back to V1.
const protocolVersion = process?.env && process.env.DEVTUNNELS_PROTOCOL_VERSION;
const connectionProtocols =
    protocolVersion === '1' ? [webSocketSubProtocol] :
    protocolVersion === '2' ? [webSocketSubProtocolv2] :
    [webSocketSubProtocolv2, webSocketSubProtocol];

/**
 * Tunnel host implementation that uses data-plane relay
 *  to accept client connections.
 */
export class TunnelRelayTunnelHost extends TunnelConnectionSession implements TunnelHost {
    public static readonly webSocketSubProtocol = webSocketSubProtocol;
    public static readonly webSocketSubProtocolv2 = webSocketSubProtocolv2;

    /**
     * Ssh channel type in host relay ssh session where client session streams are passed.
     */
    public static clientStreamChannelType: string = 'client-ssh-session-stream';

    private readonly id: string;
    private readonly hostId: string;
    private readonly clientSessionPromises: Promise<void>[] = [];
    private readonly reconnectableSessions: SshServerSession[] = [];

    /**
     * Sessions created between this host and clients
     * @internal
     */
    public readonly sshSessions: SshServerSession[] = [];

    /**
     * Port Forwarders between host and clients
     */
    public readonly remoteForwarders = new Map<string, RemotePortForwarder>();

    /**
     * Private key used for connections.
     */
    public hostPrivateKey?: KeyPair;

    /**
     * Public keys used for connections.
     */
    public hostPublicKeys?: string[];

    /**
     * Promise task to get private key used for connections.
     */
    public hostPrivateKeyPromise?: Promise<KeyPair>;

    private loopbackIp = '127.0.0.1';

    private forwardConnectionsToLocalPortsValue: boolean = isNode();

    /**
     * Synthetic endpoint signature of the endpoint created when host connects.
     * undefined if the endpoint has not been created yet.
     */
    private endpointSignature?: string;

    public constructor(managementClient: TunnelManagementClient, trace?: Trace) {
        super(TunnelAccessScopes.Host, connectionProtocols, managementClient, trace);
        const publicKey = SshAlgorithms.publicKey.ecdsaSha2Nistp384!;
        if (publicKey) {
            this.hostPrivateKeyPromise = publicKey.generateKeyPair();
        }

        this.hostId = MultiModeTunnelHost.hostId;
        this.id = uuidv4() + "-relay";
    }

    protected override get connectionId() {
        return this.hostId;
    }

    /**
     * A value indicating whether the port-forwarding service forwards connections to local TCP sockets.
     * Forwarded connections are not possible if the host is not NodeJS (e.g. browser).
     * The default value for NodeJS hosts is true.
     */
    public get forwardConnectionsToLocalPorts(): boolean {
        return this.forwardConnectionsToLocalPortsValue;
    }

    public set forwardConnectionsToLocalPorts(value: boolean) {
        if (value === this.forwardConnectionsToLocalPortsValue) {
            return;
        }

        if (value && !isNode()) {
            throw new Error('Cannot forward connections to local TCP sockets on this platform.');
        }

        this.forwardConnectionsToLocalPortsValue = value;
    }

    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     * @deprecated Use `connect()` instead.
     */
    public async start(tunnel: Tunnel): Promise<void> {
        await this.connect(tunnel);
    }

    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     */
    public async connect(
        tunnel: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<void> {
        await this.connectTunnelSession(tunnel, options, cancellation);
    }

    /**
     * Connect to the tunnel session with the tunnel connector.
     * @param tunnel Tunnel to use for the connection.
     *     Undefined if the connection information is already known and the tunnel is not needed.
     *     Tunnel object to get the connection information from that tunnel.
     */
    public async connectTunnelSession(
        tunnel?: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken
    ): Promise<void> {
        if (this.disconnectReason === SshDisconnectReason.tooManyConnections) {
            // If another host for the same tunnel connects, the first connection is disconnected
            // with "too many connections" reason. Reconnecting it again would cause the second host to
            // be kicked out, and then it would try to reconnect, kicking out this one.
            // To prevent this tug of war, do not allow reconnection in this case.
            throw new SshConnectionError(
                'Cannot retry connection because another host for this tunnel has connected. ' +
                'Only one host connection at a time is supported.',
                SshDisconnectReason.tooManyConnections);
        }

        await super.connectTunnelSession(tunnel, options, cancellation);
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
        let session: SshClientSession;
        if (this.connectionProtocol === webSocketSubProtocol) {
            // The V1 protocol always configures no security, equivalent to SSH MultiChannelStream.
            // The websocket transport is still encrypted and authenticated.
            session = new SshClientSession(
                new SshSessionConfiguration(false)); // no encryption
        } else {
            session = SshHelpers.createSshClientSession((config) => {
                // The V2 protocol configures optional encryption, including "none" as an enabled
                // and preferred key-exchange algorithm, because encryption of the outer SSH
                // session is optional since it is already over a TLS websocket.
                config.keyExchangeAlgorithms.splice(0, 0, SshAlgorithms.keyExchange.none);

                config.addService(PortForwardingService);
            });

            const hostPfs = session.activateService(PortForwardingService);
            hostPfs.messageFactory = this;
            hostPfs.onForwardedPortConnecting(this.onForwardedPortConnecting, this, this.sshSessionDisposables);
        }

        session.onChannelOpening(this.hostSession_ChannelOpening, this, this.sshSessionDisposables);
        session.onClosed(this.onSshSessionClosed, this, this.sshSessionDisposables);

        session.trace = this.trace;
        session.onReportProgress(
            (args) => this.raiseReportProgress(args.progress, args.sessionNumber),
            this,
            this.sshSessionDisposables);
        this.sshSession = session;
        await session.connect(stream, cancellation);

        // SSH authentication is skipped in V1 protocol, optional in V2 depending on whether the
        // session performed a key exchange (as indicated by having a session ID or not). In the
        // latter case a password is not required. Strong authentication was already handled by
        // the relay service via the tunnel access token used for the websocket connection.
        if (session.sessionId) {
            await session.authenticate({ username: 'tunnel' });
        }

        if (this.connectionProtocol === webSocketSubProtocolv2) {
            // In the v2 protocol, the host starts "forwarding" the ports as soon as it connects.
            // Then the relay will forward the forwarded ports to clients as they connect.
            await this.startForwardingExistingPorts(session);
        }
    }

    /**
     * Validate the {@link tunnel} and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     * @internal
     */
    public async onConnectingToTunnel(): Promise<void> {
        if (!this.hostPrivateKey || !this.hostPublicKeys) {
            if (!this.hostPrivateKeyPromise) {
                throw new Error('Cannot create host keys');
            }
            this.hostPrivateKey = await this.hostPrivateKeyPromise;
            const buffer = await this.hostPrivateKey.getPublicKeyBytes(
                this.hostPrivateKey.keyAlgorithmName,
            );
            if (!buffer) {
                throw new Error('Host private key public key bytes is not initialized');
            }
            this.hostPublicKeys = [buffer.toString('base64')];
        }

        const tunnelHasSshPort = this.tunnel?.ports != null && this.tunnel.ports.find((v) => v.protocol === TunnelProtocol.Ssh);
        const endpointSignature = 
            `${this.tunnel?.tunnelId}.${this.tunnel?.clusterId}:` +
            `${this.tunnel?.name}.${this.tunnel?.domain}:` +
            `${tunnelHasSshPort}:${this.hostId}:${this.hostPublicKeys}`;

        if (!this.relayUri || this.endpointSignature !== endpointSignature) {
            if (!this.tunnel) {
                throw new Error('Tunnel is required');
            }
    
            let endpoint: TunnelRelayTunnelEndpoint = {
                id: this.id,
                hostId: this.hostId,
                hostPublicKeys: this.hostPublicKeys,
                connectionMode: TunnelConnectionMode.TunnelRelay,
            };
            
            let additionalQueryParameters = undefined;
            if (tunnelHasSshPort) {
                additionalQueryParameters = { includeSshGatewayPublicKey: 'true' };
            }
    
            endpoint = await this.managementClient!.updateTunnelEndpoint(this.tunnel, endpoint, {
                additionalQueryParameters: additionalQueryParameters,
            });

            this.relayUri = endpoint.hostRelayUri!;
            this.endpointSignature = endpointSignature;
        }
    }

    /**
     * Disposes this tunnel session, closing all client connections, the host SSH session, and deleting the endpoint.
     */
    public async dispose(): Promise<void> {
        await super.dispose();

        const promises: Promise<any>[] = Object.assign([], this.clientSessionPromises);

        // No new client session should be added because the channel requests are rejected when the tunnel host is disposed.
        this.clientSessionPromises.length = 0;

        // If the tunnel is present, the endpoint was created, and this host was not closed because of
        // too many connections, delete the endpoint.
        // Too many connections closure means another host has connected, and that other host, while
        // connecting, would have updated the endpoint. So this host won't be able to delete it anyway.
        if (this.tunnel &&
            this.endpointSignature &&
            this.disconnectReason !== SshDisconnectReason.tooManyConnections) {
            const promise = this.managementClient!.deleteTunnelEndpoints(
                this.tunnel,
                this.id,
            );
            promises.push(promise);
        }

        for (const forwarder of this.remoteForwarders.values()) {
            forwarder.dispose();
        }

        // When client session promises finish, they remove the sessions from this.sshSessions
        await Promise.all(promises);
    }

    private hostSession_ChannelOpening(e: SshChannelOpeningEventArgs) {
        if (!e.isRemoteRequest) {
            // Auto approve all local requests (not that there are any for the time being).
            return;
        }

        if (this.connectionProtocol === webSocketSubProtocolv2 &&
            e.channel.channelType === 'forwarded-tcpip'
        ) {
            // With V2 protocol, the relay server always sends an extended channel open message
            // with a property indicating whether E2E encryption is requested for the connection.
            // The host returns an extended response message indicating if E2EE is enabled.
            const relayRequestMessage = e.channel.openMessage
                .convertTo(new PortRelayConnectRequestMessage());
            const responseMessage = new PortRelayConnectResponseMessage();

            // The host can enable encryption for the channel if the client requested it.
            responseMessage.isE2EEncryptionEnabled = this.enableE2EEncryption &&
                relayRequestMessage.isE2EEncryptionRequested;

            // In the future the relay might send additional information in the connect
            // request message, for example a user identifier that would enable the host to
            // group channels by user.

            e.openingPromise = Promise.resolve(responseMessage);
            return;
        } else if (e.channel.channelType !== TunnelRelayTunnelHost.clientStreamChannelType) {
            e.failureDescription = `Unknown channel type: ${e.channel.channelType}`;
            e.failureReason = SshChannelOpenFailureReason.unknownChannelType;
            return;
        }

        // V1 protocol.

        // Increase max window size to work around channel congestion bug.
        // This does not entirely eliminate the problem, but reduces the chance.
        e.channel.maxWindowSize = SshChannel.defaultMaxWindowSize * 5;

        if (this.isDisposed) {
            e.failureDescription = 'The host is disconnecting.';
            e.failureReason = SshChannelOpenFailureReason.connectFailed;
            return;
        }

        const promise = this.acceptClientSession(e.channel, this.disposeToken);
        this.clientSessionPromises.push(promise);

        // eslint-disable-next-line @typescript-eslint/no-floating-promises
        promise.then(() => {
            const index = this.clientSessionPromises.indexOf(promise);
            this.clientSessionPromises.splice(index, 1);
        });
    }

    protected onForwardedPortConnecting(e: ForwardedPortConnectingEventArgs): void {
        const channel = e.stream.channel;
        const relayRequestMessage = channel.openMessage.convertTo(
            new PortRelayConnectRequestMessage());

        const isE2EEncryptionEnabled = this.enableE2EEncryption &&
            relayRequestMessage.isE2EEncryptionRequested;
        if (isE2EEncryptionEnabled) {
            // Increase the max window size so that it is at least larger than the window
            // size of one client channel.
            channel.maxWindowSize = SshChannel.defaultMaxWindowSize * 2;

            const serverCredentials: SshServerCredentials = {
                publicKeys: [this.hostPrivateKey!]
            };
            const secureStream = new SecureStream(
                e.stream,
                serverCredentials,
                this.reconnectableSessions);
            secureStream.trace = this.trace;

            // The client was already authenticated by the relay.
            secureStream.onAuthenticating((authEvent) =>
                authEvent.authenticationPromise = Promise.resolve({}));

            // The client will connect to the secure stream after the channel is opened.
            secureStream.connect().catch((err) => {
                this.trace(TraceLevel.Error, 0, `Error connecting encrypted channel: ${err}`);
            });

            e.transformPromise = Promise.resolve(secureStream);
        }

        super.onForwardedPortConnecting(e);
    }

    private async acceptClientSession(
        clientSessionChannel: SshChannel,
        cancellation: CancellationToken,
    ): Promise<void> {
        try {
            const stream = new SshStream(clientSessionChannel);
            await this.connectAndRunClientSession(stream, cancellation);
        } catch (ex) {
            if (!(ex instanceof CancellationError) || !cancellation.isCancellationRequested) {
                this.trace(TraceLevel.Error, 0, `Error running client SSH session: ${ex}`);
            }
        }
    }

    /**
     * Creates an SSH server session for a client (V1 protocol), runs the session,
     * and waits for it to close.
     */
    private async connectAndRunClientSession(
        stream: SshStream,
        cancellation: CancellationToken,
    ): Promise<void> {
        if (cancellation.isCancellationRequested) {
            stream.destroy();
            throw new CancellationError();
        }

        const clientChannelId = stream.channel.channelId;

        const session = SshHelpers.createSshServerSession(this.reconnectableSessions, (config) => {
            config.protocolExtensions.push(SshProtocolExtensionNames.sessionReconnect);
            config.addService(PortForwardingService);

            // Configure keep-alive if requested
            const keepAliveInterval = this.connectionOptions?.keepAliveIntervalInSeconds;
            if (keepAliveInterval && keepAliveInterval > 0) {
                config.keepAliveTimeoutInSeconds = keepAliveInterval;
            }
        });
        session.trace = this.trace;
        session.onReportProgress(
            (args) => this.raiseReportProgress(args.progress, args.sessionNumber),
            this,
            this.sshSessionDisposables);
        session.credentials = {
            publicKeys: [this.hostPrivateKey!],
        };

        const tcs = new PromiseCompletionSource<void>();

        const authenticatingEventRegistration = session.onAuthenticating((e) => {
            this.onSshClientAuthenticating(e);
        });
        session.onClientAuthenticated(() => {
            // This call is async and will catch and log any async errors.
            void this.onSshClientAuthenticated(session);
        });
        const requestRegistration = session.onRequest((e) => {
            this.onClientSessionRequest(e, session);
        });
        const channelOpeningEventRegistration = session.onChannelOpening((e) => {
            this.onSshChannelOpening(e, session);
        });
        const reconnectedEventRegistration = session.onReconnected(() => {
            this.onClientSessionReconnecting(session, clientChannelId);
        })
        const closedEventRegistration = session.onClosed((e) => {
            this.onClientSessionClosed(session, e, clientChannelId, cancellation);
            tcs.resolve();
        });

        session.onKeepAliveFailed((count) => this.onKeepAliveFailed(count));
        session.onKeepAliveSucceeded((count) => this.onKeepAliveSucceeded(count));

        try {
            const nodeStream = new NodeStream(stream);
            await session.connect(nodeStream);
            this.sshSessions.push(session);
            cancellation.onCancellationRequested((e) => {
                tcs.reject(new CancellationError());
            });

            if (this.tunnel && this.managementClient) {
                const connectedEvent: TunnelEvent = {
                    name: 'host_client_connect',
                    properties: {
                        ClientChannelId: clientChannelId.toString(),
                        ClientSessionId: this.getShortSessionId(session),
                        HostSessionId: this.connectionId,
                    }
                };
                this.managementClient.reportEvent(this.tunnel, connectedEvent);
            }

            await tcs.promise;
        } finally {
            authenticatingEventRegistration.dispose();
            requestRegistration.dispose();
            channelOpeningEventRegistration.dispose();
            reconnectedEventRegistration.dispose();
            closedEventRegistration.dispose();

            await session.close(SshDisconnectReason.byApplication);
            session.dispose();
        }
    }

    private onSshClientAuthenticating(e: SshAuthenticatingEventArgs) {
        if (e.authenticationType === SshAuthenticationType.clientNone) {
            // For now, the client is allowed to skip SSH authentication;
            // they must have a valid tunnel access token already to get this far.
            e.authenticationPromise = Promise.resolve({});
        } else {
            // Other authentication types are not implemented. Doing nothing here
            // results in a client authentication failure.
        }
    }

    private async onSshClientAuthenticated(session: SshServerSession) {
        void this.startForwardingExistingPorts(session);
    }

    private async startForwardingExistingPorts(session: SshSession): Promise<void> {
        const pfs = session.activateService(PortForwardingService);
        pfs.forwardConnectionsToLocalPorts = this.forwardConnectionsToLocalPorts;

        // Ports must be forwarded sequentially because the TS SSH lib
        // does not yet support concurrent requests.
        for (const port of this.tunnel?.ports ?? []) {
            this.trace(TraceLevel.Verbose, 0, `Forwarding port ${port.portNumber}`);
            try {
                await this.forwardPort(pfs, port);
            } catch (ex) {
                this.traceError(`Error forwarding port ${port.portNumber}: ${ex}`);
            }
        }
    }

    private onClientSessionRequest(e: SshRequestEventArgs<SessionRequestMessage>, session: any) {
        if (e.requestType === 'RefreshPorts') {
            e.responsePromise = (async () => {
                await this.refreshPorts();
                return new SessionRequestSuccessMessage();
            })();
        }
    }

    private onSshChannelOpening(e: SshChannelOpeningEventArgs, session: any) {
        if (!(e.request instanceof PortForwardChannelOpenMessage)) {
            // This is to let the Go SDK open an unused session channel
            if (e.request.channelType === SshChannel.sessionChannelType) {
                return;
            }
            this.trace(
                TraceLevel.Warning,
                0,
                'Rejecting request to open non-portforwarding channel.',
            );
            e.failureReason = SshChannelOpenFailureReason.administrativelyProhibited;
            return;
        }
        const portForwardRequest = e.request as PortForwardChannelOpenMessage;
        if (portForwardRequest.channelType === 'direct-tcpip') {
            if (!this.tunnel!.ports!.some((p) => p.portNumber === portForwardRequest.port)) {
                this.trace(
                    TraceLevel.Warning,
                    0,
                    'Rejecting request to connect to non-forwarded port:' + portForwardRequest.port,
                );
                e.failureReason = SshChannelOpenFailureReason.administrativelyProhibited;
            }
        } else if (portForwardRequest.channelType === 'forwarded-tcpip') {
            const eventArgs = new ForwardedPortConnectingEventArgs(
                portForwardRequest.port, false, new SshStream(e.channel));
            super.onForwardedPortConnecting(eventArgs);
        } else {
            // For forwarded-tcpip do not check remoteForwarders because they may not be updated yet.
            // There is a small time interval in forwardPort() between the port
            // being forwarded with forwardFromRemotePort and remoteForwarders updated.
            // Setting PFS.acceptRemoteConnectionsForNonForwardedPorts to false makes PFS reject forwarding requests from the
            // clients for the ports that are not forwarded and are missing in PFS.remoteConnectors.
            // Call to pfs.forwardFromRemotePort() in forwardPort() adds the connector to PFS.remoteConnectors.
            this.trace(
                TraceLevel.Warning,
                0,
                'Nonrecognized channel type ' + portForwardRequest.channelType,
            );
            e.failureReason = SshChannelOpenFailureReason.unknownChannelType;
        }
    }

    private onClientSessionReconnecting(session: SshServerSession, clientChannelId: number) {
        if (this.tunnel && this.managementClient) {
            const reconnectedEvent: TunnelEvent = {
                name: 'host_client_reconnect',
                properties: {
                    ClientChannelId: clientChannelId.toString(),
                    ClientSessionId: this.getShortSessionId(session),
                    HostSessionId: this.connectionId,
                }
            };
            this.managementClient.reportEvent(this.tunnel, reconnectedEvent);
        }
    }

    private onClientSessionClosed(
        session: SshServerSession,
        e: SshSessionClosedEventArgs,
        clientChannelId: number,
        cancellation: CancellationToken,
    ) {
        // Determine severity based on the disconnect reason
        let severity: string | undefined;
        let details: string;
        
        // Reconnecting client session may cause the new session to close with 'None' reason.
        if (e.reason === SshDisconnectReason.byApplication) {
            details = 'Client ssh session closed by application.';
            this.traceInfo(details);
        } else if (cancellation.isCancellationRequested) {
            details = 'Client ssh session cancelled.';
            this.traceInfo(details);
        } else if (e.reason !== SshDisconnectReason.none) {
            severity = TunnelEvent.error;
            details = `Client ssh session closed unexpectedly due to ${e.reason}, ` +
                `"${e.message}"\n${e.error}`;
            this.traceError(details);
        } else {
            details = 'Client ssh session closed.';
        }

        // Report client disconnected event
        if (this.tunnel && this.managementClient) {
            const disconnectedEvent: TunnelEvent = {
                timestamp: new Date(),
                name: 'host_client_disconnect',
                severity: severity,
                details: details,
                properties: {
                    ClientChannelId: clientChannelId.toString(),
                    ClientSessionId: this.getShortSessionId(session),
                    HostSessionId: this.connectionId,
                }
            };
            this.managementClient.reportEvent(this.tunnel, disconnectedEvent);
        }

        for (const [key, forwarder] of this.remoteForwarders.entries()) {
            if (forwarder.session === session) {
                forwarder.dispose();
                this.remoteForwarders.delete(key);
            }
        }

        const index = this.sshSessions.indexOf(session);
        if (index >= 0) {
            this.sshSessions.splice(index, 1);
        }
    }

    public async refreshPorts(cancellation?: CancellationToken): Promise<void> {
        this.raiseReportProgress(TunnelProgress.StartingRefreshPorts);
        if (!await this.refreshTunnel(true, cancellation)) {
            return;
        }

        const ports = this.tunnel?.ports ?? [];

        let sessions: SshSession[] = this.sshSessions;
        if (this.connectionProtocol === webSocketSubProtocolv2 && this.sshSession) {
            // In the V2 protocol, ports are forwarded directly on the host session.
            // (But even when the host is V2, some clients may still connect with V1.)
            sessions = [...sessions, this.sshSession ];
        }

        const forwardPromises: Promise<any>[] = [];

        for (const port of ports) {
            // For all sessions which are connected and authenticated, forward any added/updated
            // ports. For sessions that are not yet authenticated, the ports will be forwarded
            // immediately after authentication completes - see onSshClientAuthenticated().
            // (Session requests may not be sent before the session is authenticated, for sessions
            // that require authentication; For V2 sessions that are not encrypted/authenticated
            // at all, the session ID is null.)
            for (const session of sessions.filter(
                    (s) => s.isConnected && (!s.sessionId || s.principal))) {
                const key = new SessionPortKey(session.sessionId, Number(port.portNumber));
                const forwarder = this.remoteForwarders.get(key.toString());
                if (!forwarder) {
                    const pfs = session.getService(PortForwardingService)!;
                    forwardPromises.push(this.forwardPort(pfs, port));
                }
            }
        }

        for (const [key, forwarder] of Object.entries(this.remoteForwarders)) {
            if (!ports.some((p) => p.portNumber === forwarder.localPort)) {
                this.remoteForwarders.delete(key);
                forwarder.dispose();
            }
        }

        await Promise.all(forwardPromises);
        this.raiseReportProgress(TunnelProgress.CompletedRefreshPorts);
    }

    protected async forwardPort(pfs: PortForwardingService, port: TunnelPort) {
        const portNumber = Number(port.portNumber);
        if (pfs.localForwardedPorts.find((p) => p.localPort === portNumber)) {
            // The port is already forwarded. This may happen if we try to add the same port twice after reconnection.
            return;
        }

        // When forwarding from a Remote port we assume that the RemotePortNumber
        // and requested LocalPortNumber are the same.
        const forwarder = await pfs.forwardFromRemotePort(
            this.loopbackIp,
            portNumber,
            'localhost',
            portNumber,
        );
        if (!forwarder) {
            // The forwarding request was rejected by the client.
            return;
        }

        const key = new SessionPortKey(pfs.session.sessionId, Number(forwarder.localPort));
        this.remoteForwarders.set(key.toString(), forwarder);
    }
}
