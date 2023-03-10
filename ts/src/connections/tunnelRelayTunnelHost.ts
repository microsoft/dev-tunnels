// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    Tunnel,
    TunnelAccessScopes,
    TunnelConnectionMode,
    TunnelProtocol,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import {
    MultiChannelStream,
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
} from '@microsoft/dev-tunnels-ssh';
import {
    PortForwardChannelOpenMessage,
    PortForwardingService,
} from '@microsoft/dev-tunnels-ssh-tcp';
import { CancellationToken, Disposable } from 'vscode-jsonrpc';
import { SshHelpers } from './sshHelpers';
import { MultiModeTunnelHost } from './multiModeTunnelHost';
import { TunnelHostBase } from './tunnelHostBase';
import { tunnelRelaySessionClass } from './tunnelRelaySessionClass';

const webSocketSubProtocol = 'tunnel-relay-host';

/**
 * Tunnel host implementation that uses data-plane relay
 *  to accept client connections.
 */
export class TunnelRelayTunnelHost extends tunnelRelaySessionClass(
    TunnelHostBase,
    webSocketSubProtocol,
) {
    /**
     * Ssh channel type in host relay ssh session where client session streams are passed.
     */
    public static clientStreamChannelType: string = 'client-ssh-session-stream';

    private readonly hostId: string;
    private readonly clientSessionPromises: Promise<void>[] = [];
    private readonly reconnectableSessions: SshServerSession[] = [];

    constructor(managementClient: TunnelManagementClient, trace?: Trace) {
        super(managementClient, trace);
        this.hostId = MultiModeTunnelHost.hostId;
    }

    /**
     * Configures the tunnel session with the given stream.
     * @internal
     */
    public async configureSession(
        stream: Stream,
        isReconnect: boolean,
        cancellation: CancellationToken,
    ): Promise<void> {
        this.sshSession = new MultiChannelStream(stream);

        // Increase max window size to work around channel congestion bug.
        // This does not entirely eliminate the problem, but reduces the chance.
        // TODO: Change the protocol to avoid layering SSH sessions, which will resolve the issue.
        this.sshSession.channelMaxWindowSize = SshChannel.defaultMaxWindowSize * 5;

        const channelOpenEventRegistration = this.sshSession.onChannelOpening((e) => {
            this.hostSession_ChannelOpening(this.sshSession!, e);
        });
        const closeEventRegistration = this.sshSession.onClosed((e) => {
            this.hostSession_Closed(e, channelOpenEventRegistration, closeEventRegistration);
        });

        await this.sshSession.connect();
    }

    /**
     * Gets the tunnel relay URI.
     * @internal
     */
    public async getTunnelRelayUri(tunnel?: Tunnel): Promise<string> {
        if (!tunnel) {
            throw new Error('Tunnel is required');
        }

        let endpoint: TunnelRelayTunnelEndpoint = {
            hostId: this.hostId,
            hostPublicKeys: this.hostPublicKeys,
            connectionMode: TunnelConnectionMode.TunnelRelay,
        };
        let additionalQueryParameters = undefined;
        if (tunnel.ports != null && tunnel.ports.find((v) => v.protocol === TunnelProtocol.Ssh)) {
            additionalQueryParameters = { includeSshGatewayPublicKey: 'true' };
        }

        endpoint = await this.managementClient!.updateTunnelEndpoint(tunnel, endpoint, {
            additionalQueryParameters: additionalQueryParameters,
        });
        return endpoint.hostRelayUri!;
    }

    private hostSession_ChannelOpening(sender: MultiChannelStream, e: SshChannelOpeningEventArgs) {
        if (!e.isRemoteRequest) {
            // Auto approve all local requests (not that there are any for the time being).
            return;
        }

        if (e.channel.channelType !== TunnelRelayTunnelHost.clientStreamChannelType) {
            e.failureDescription = `Unexpected channel type. Only ${TunnelRelayTunnelHost.clientStreamChannelType} is supported.`;
            e.failureReason = SshChannelOpenFailureReason.unknownChannelType;
            return;
        }

        if (this.isDisposed) {
            e.failureDescription = 'The host is disconnecting.';
            e.failureReason = SshChannelOpenFailureReason.connectFailed;
            return;
        }

        const promise = this.acceptClientSession(sender, this.disposeToken);
        this.clientSessionPromises.push(promise);

        promise.then(() => {
            const index = this.clientSessionPromises.indexOf(promise);
            this.clientSessionPromises.splice(index, 1);
        });
    }

    private async acceptClientSession(
        hostSession: MultiChannelStream,
        cancellation: CancellationToken,
    ): Promise<void> {
        try {
            let stream = await hostSession.acceptStream(
                TunnelRelayTunnelHost.clientStreamChannelType,
                cancellation,
            );
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

        let session = SshHelpers.createSshServerSession(this.reconnectableSessions, (config) => {
            config.addService(PortForwardingService);
        });
        session.trace = this.trace;
        session.credentials = {
            publicKeys: [this.hostPrivateKey!],
        };

        let tcs = new PromiseCompletionSource<void>();
        cancellation.onCancellationRequested((e) => {
            tcs.reject(new CancellationError());
        });

        const authenticatingEventRegistration = session.onAuthenticating((e) => {
            this.onSshClientAuthenticating(e);
        });
        session.onClientAuthenticated(() => {
            this.onSshClientAuthenticated(session);
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

    private onSshClientAuthenticated(session: SshServerSession) {
        let pfs = session.activateService(PortForwardingService);
        pfs.forwardConnectionsToLocalPorts = this.forwardConnectionsToLocalPorts;

        let ports = this.tunnel?.ports ?? [];
        ports.forEach(async (port) => {
            try {
                await this.forwardPort(pfs, port);
            } catch (ex) {
                this.traceError(`Error forwarding port ${port.portNumber}: ${ex}`);
            }
        });
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
        let portForwardRequest = e.request as PortForwardChannelOpenMessage;
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
            this.onSshChannelOpen(portForwardRequest.port, e.channel);
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
                `Client ssh session closed unexpectely due to ${e.reason}, \"${e.message}\"\n${e.error}`,
            );
        }

        for (const key of Object.keys(this.remoteForwarders)) {
            const forwarder = this.remoteForwarders[key];
            if (forwarder.session === session) {
                forwarder.dispose();
                delete this.remoteForwarders[key];
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

    /**
     * Disposes this tunnel session, closing all client connections, the host SSH session, and deleting the endpoint.
     */
    public async dispose(): Promise<void> {
        await super.dispose();

        let promises: Promise<any>[] = Object.assign([], this.clientSessionPromises);

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

        for (const key of Object.keys(this.remoteForwarders)) {
            this.remoteForwarders[key].dispose();
        }

        // When client session promises finish, they remove the sessions from this.sshSessions
        await Promise.all(promises);
    }
}
