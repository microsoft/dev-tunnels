// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelConnectionMode,
    TunnelProtocol,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import {
    SshChannelOpeningEventArgs,
    SshChannelOpenFailureReason,
    SshStream,
    SshSessionClosedEventArgs,
    SshDisconnectReason,
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
} from '@microsoft/dev-tunnels-ssh';
import {
    ForwardedPortConnectingEventArgs,
    PortForwardChannelOpenMessage,
    PortForwardingService,
} from '@microsoft/dev-tunnels-ssh-tcp';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { SshHelpers } from './sshHelpers';
import { MultiModeTunnelHost } from './multiModeTunnelHost';
import { TunnelHostBase } from './tunnelHostBase';
import { tunnelRelaySessionClass } from './tunnelRelaySessionClass';
import { SessionPortKey } from './sessionPortKey';
import { PortRelayConnectRequestMessage } from './messages/portRelayConnectRequestMessage';
import { PortRelayConnectResponseMessage } from './messages/portRelayConnectResponseMessage';

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
export class TunnelRelayTunnelHost extends tunnelRelaySessionClass(
    TunnelHostBase,
    connectionProtocols,
) {
    public static readonly webSocketSubProtocol = webSocketSubProtocol;
    public static readonly webSocketSubProtocolv2 = webSocketSubProtocolv2;

    /**
     * Ssh channel type in host relay ssh session where client session streams are passed.
     */
    public static clientStreamChannelType: string = 'client-ssh-session-stream';

    private readonly hostId: string;
    private readonly clientSessionPromises: Promise<void>[] = [];
    private readonly reconnectableSessions: SshServerSession[] = [];

    public constructor(managementClient: TunnelManagementClient, trace?: Trace) {
        super(managementClient, trace);
        this.hostId = MultiModeTunnelHost.hostId;
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
        if (this.connectionProtocol === webSocketSubProtocol) {
            // The V1 protocol always configures no security, equivalent to SSH MultiChannelStream.
            // The websocket transport is still encrypted and authenticated.
            this.sshSession = new SshClientSession(
                new SshSessionConfiguration(false)); // no encryption
        } else {
            // The V2 protocol configures optional encryption, including "none" as an enabled and
            // preferred key-exchange algorithm, because encryption of the outer SSH session is
            // optional since it is already over a TLS websocket.
            const config = new SshSessionConfiguration();
            config.keyExchangeAlgorithms.splice(
                0,
                config.keyExchangeAlgorithms.length,
                SshAlgorithms.keyExchange.none,
                SshAlgorithms.keyExchange.ecdhNistp384Sha384,
                SshAlgorithms.keyExchange.ecdhNistp256Sha256,
                SshAlgorithms.keyExchange.dhGroup16Sha512,
                SshAlgorithms.keyExchange.dhGroup14Sha256,
            );

            config.addService(PortForwardingService);
            this.sshSession = new SshClientSession(config);

            const hostPfs = this.sshSession.activateService(PortForwardingService);
            hostPfs.messageFactory = this;
            hostPfs.onForwardedPortConnecting((e) => this.onForwardedPortConnecting(e));
        }

        const channelOpenEventRegistration = this.sshSession.onChannelOpening((e) => {
            this.hostSession_ChannelOpening(this.sshSession!, e);
        });
        const closeEventRegistration = this.sshSession.onClosed((e) => {
            this.hostSession_Closed(e, channelOpenEventRegistration, closeEventRegistration);
        });

        this.sshSession.trace = this.trace;
        await this.sshSession.connect(stream, cancellation);

        // SSH authentication is skipped in V1 protocol, optional in V2 depending on whether the
        // session performed a key exchange (as indicated by having a session ID or not). In the
        // latter case a password is not required. Strong authentication was already handled by
        // the relay service via the tunnel access token used for the websocket connection.
        if (this.sshSession.sessionId) {
            await this.sshSession.authenticate({ username: 'tunnel' });
        }

        if (this.connectionProtocol === webSocketSubProtocolv2) {
            // In the v2 protocol, the host starts "forwarding" the ports as soon as it connects.
            // Then the relay will forward the forwarded ports to clients as they connect.
            await this.startForwardingExistingPorts(this.sshSession);
        }
    }

    public async onConnectingToTunnel(): Promise<void> {
        await super.onConnectingToTunnel();
        if (!this.relayUri) {
            if (!this.tunnel) {
                throw new Error('Tunnel is required');
            }
    
            let endpoint: TunnelRelayTunnelEndpoint = {
                hostId: this.hostId,
                hostPublicKeys: this.hostPublicKeys,
                connectionMode: TunnelConnectionMode.TunnelRelay,
            };
            
            let additionalQueryParameters = undefined;
            if (this.tunnel.ports != null && this.tunnel.ports.find((v) => v.protocol === TunnelProtocol.Ssh)) {
                additionalQueryParameters = { includeSshGatewayPublicKey: 'true' };
            }
    
            endpoint = await this.managementClient!.updateTunnelEndpoint(this.tunnel, endpoint, {
                additionalQueryParameters: additionalQueryParameters,
            });
            
            this.relayUri = endpoint.hostRelayUri!;
        }
    }

    private hostSession_ChannelOpening(sender: SshClientSession, e: SshChannelOpeningEventArgs) {
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

    private async connectAndRunClientSession(
        stream: SshStream,
        cancellation: CancellationToken,
    ): Promise<void> {
        if (cancellation.isCancellationRequested) {
            stream.destroy();
            throw new CancellationError();
        }

        const session = SshHelpers.createSshServerSession(this.reconnectableSessions, (config) => {
            config.addService(PortForwardingService);
        });
        session.trace = this.trace;
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
            this.onSshSessionRequest(e, session);
        });
        const channelOpeningEventRegistration = session.onChannelOpening((e) => {
            this.onSshChannelOpening(e, session);
        });
        const closedEventRegistration = session.onClosed((e) => {
            this.session_Closed(session, e, cancellation);
            tcs.resolve();
        });

        try {
            const nodeStream = new NodeStream(stream);
            await session.connect(nodeStream);
            this.sshSessions.push(session);
            cancellation.onCancellationRequested((e) => {
                tcs.reject(new CancellationError());
            });
            await tcs.promise;
        } finally {
            authenticatingEventRegistration.dispose();
            requestRegistration.dispose();
            channelOpeningEventRegistration.dispose();
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

    private onSshSessionRequest(e: SshRequestEventArgs<SessionRequestMessage>, session: any) {
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

    private session_Closed(
        session: SshServerSession,
        e: SshSessionClosedEventArgs,
        cancellation: CancellationToken,
    ) {
        if (e.reason === SshDisconnectReason.byApplication) {
            this.traceInfo('Client ssh session closed.');
        } else if (cancellation.isCancellationRequested) {
            this.traceInfo('Client ssh session cancelled.');
        } else {
            this.traceError(
                `Client ssh session closed unexpectely due to ${e.reason}, "${e.message}"\n${e.error}`,
            );
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

    private hostSession_Closed(
        e: SshSessionClosedEventArgs,
        channelOpenEventRegistration: Disposable,
        closeEventRegistration: Disposable,
    ) {
        closeEventRegistration.dispose();
        channelOpenEventRegistration.dispose();
        this.sshSession = undefined;
        this.traceInfo(
            `Connection to host tunnel relay closed.${this.isDisposed ? '' : ' Reconnecting.'}`,
        );

        if (e.reason === SshDisconnectReason.connectionLost) {
            this.startReconnectingIfNotDisposed();
        }
    }

    public async refreshPorts(cancellation?: CancellationToken): Promise<void> {
        if (!this.canRefreshTunnel) {
            return;
        }

        await this.refreshTunnel(cancellation);
        const ports = this.tunnel?.ports ?? [];

        let sessions: SshSession[] = this.sshSessions;
        if (this.connectionProtocol === webSocketSubProtocolv2 && this.sshSession) {
            // In the V2 protocol, ports are forwarded directly on the host session.
            // (But even when the host is V2, some clients may still connect with V1.)
            sessions = [...sessions, this.sshSession ];
        }

        const forwardPromises: Promise<any>[] = [];

        for (const port of ports) {
            for (const session of sessions.filter((s) => s.isConnected && s.sessionId)) {
                const key = new SessionPortKey(session.sessionId!, Number(port.portNumber));
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
    }

    /**
     * Disposes this tunnel session, closing all client connections, the host SSH session, and deleting the endpoint.
     */
    public async dispose(): Promise<void> {
        await super.dispose();

        const promises: Promise<any>[] = Object.assign([], this.clientSessionPromises);

        // No new client session should be added because the channel requests are rejected when the tunnel host is disposed.
        this.clientSessionPromises.length = 0;

        if (this.tunnel) {
            const promise = this.managementClient!.deleteTunnelEndpoints(
                this.tunnel,
                this.hostId,
                TunnelConnectionMode.TunnelRelay,
            );
            promises.push(promise);
        }

        for (const forwarder of this.remoteForwarders.values()) {
            forwarder.dispose();
        }

        // When client session promises finish, they remove the sessions from this.sshSessions
        await Promise.all(promises);
    }
}
