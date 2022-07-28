// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    Tunnel,
    TunnelAccessScopes,
    TunnelConnectionMode,
    TunnelRelayTunnelEndpoint,
} from '@vs/tunnels-contracts';
import { TunnelAccessTokenProperties, TunnelManagementClient } from '@vs/tunnels-management';
import {
    MultiChannelStream,
    SshChannelOpeningEventArgs,
    SshChannelOpenFailureReason,
    SshStream,
    SshSessionClosedEventArgs,
    SshDisconnectReason,
    TraceLevel,
    SshSessionConfiguration,
    SshServerSession,
    SshAuthenticatingEventArgs,
    NodeStream,
    SshAuthenticationType,
    PromiseCompletionSource,
    CancellationError,
    ObjectDisposedError,
} from '@vs/vs-ssh';
import { PortForwardChannelOpenMessage, PortForwardingService } from '@vs/vs-ssh-tcp';
import { CancellationToken, CancellationTokenSource, Disposable } from 'vscode-jsonrpc';
import { TunnelRelayStreamFactory, DefaultTunnelRelayStreamFactory, SessionPortKey } from '.';
import { MultiModeTunnelHost } from './multiModeTunnelHost';
import { TunnelHostBase } from './tunnelHostBase';

/**
 * Tunnel host implementation that uses data-plane relay
 *  to accept client connections.
 */
export class TunnelRelayTunnelHost extends TunnelHostBase {
    /**
     * Web socket sub-protocol to connect to the tunnel relay endpoint.
     */
    public static webSocketSubProtocol: string = 'tunnel-relay-host';

    /**
     * Ssh channel type in host relay ssh session where client session streams are passed.
     */
    public static clientStreamChannelType: string = 'client-ssh-session-stream';

    /**
     * Gets or sets a factory for creating relay streams.
     */
    public streamFactory: TunnelRelayStreamFactory = new DefaultTunnelRelayStreamFactory();

    private readonly hostId: string;
    private readonly clientSessionPromises: Promise<void>[] = [];
    private readonly disposeCts: CancellationTokenSource = new CancellationTokenSource();
    private hostSession?: MultiChannelStream;

    constructor(managementClient: TunnelManagementClient) {
        super(managementClient);
        this.hostId = MultiModeTunnelHost.hostId;
    }

    public async startServer(tunnel: Tunnel, hostPublicKeys?: string[]): Promise<void> {
        let accessToken = tunnel.accessTokens
            ? tunnel.accessTokens[TunnelAccessScopes.Host]
            : undefined;
        if (!accessToken) {
            this.trace(
                TraceLevel.Info,
                0,
                `There is no access token for ${TunnelAccessScopes.Host} scope on the tunnel.`,
            );
        }

        let endpoint: TunnelRelayTunnelEndpoint = {
            hostId: this.hostId,
            hostPublicKeys: hostPublicKeys,
            connectionMode: TunnelConnectionMode.TunnelRelay,
        };

        endpoint = await this.managementClient.updateTunnelEndpoint(tunnel, endpoint, undefined);

        this.tunnel = tunnel;

        let hostRelayUri = endpoint.hostRelayUri;
        if (!hostRelayUri) {
            throw new Error(`The tunnel host relay endpoint URI is missing.`);
        }

        this.trace(TraceLevel.Info, 0, `Connecting to host tunnel relay ${hostRelayUri}`);
        this.trace(
            TraceLevel.Verbose,
            0,
            `Sec-WebSocket-Protocol: ${TunnelRelayTunnelHost.webSocketSubProtocol}`,
        );
        if (accessToken) {
            const token = TunnelAccessTokenProperties.tryParse(accessToken)?.toString() ?? 'token';
            this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel ${token}`);
        }

        try {
            let stream = await this.streamFactory.createRelayStream(
                hostRelayUri!,
                TunnelRelayTunnelHost.webSocketSubProtocol,
                accessToken,
                this.managementClient.httpsAgent
                    ? { tlsOptions: this.managementClient.httpsAgent.options }
                    : undefined,
            );

            this.hostSession = new MultiChannelStream(stream);
            const channelOpenEventRegistration = this.hostSession.onChannelOpening((e) => {
                this.hostSession_ChannelOpening(this.hostSession!, e);
            });
            const closeEventRegistration = this.hostSession.onClosed((e) => {
                this.hostSession_Closed(channelOpenEventRegistration, closeEventRegistration);
            });
            try {
                await this.hostSession.connect();
            } catch {
                await this.hostSession.close();
                throw new Error();
            }
        } catch (exception) {
            throw new Error('Failed to connect to tunnel relay. ' + exception);
        }
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

        const promise = this.acceptClientSession(sender, this.disposeCts.token);
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
            this.trace(TraceLevel.Error, 0, `Error running client SSH session: ${ex}`);
        }
    }

    private async connectAndRunClientSession(
        stream: SshStream,
        cancellation: CancellationToken,
    ): Promise<void> {
        let serverConfig = new SshSessionConfiguration();
        serverConfig.addService(PortForwardingService);
        let session = new SshServerSession(serverConfig);
        session.trace = this.trace;
        session.credentials = {
            publicKeys: [this.hostPrivateKey!],
        };

        let tcs = new PromiseCompletionSource<any>();
        cancellation.onCancellationRequested((e) => {
            tcs.reject(new CancellationError());
        });

        const authenticatingEventRegistration = session.onAuthenticating((e) => {
            this.onSshClientAuthenticating(e);
        });
        session.onClientAuthenticated(() => {
            this.onSshClientAuthenticated(session);
        });
        const channelOpeningEventRegistration = session.onChannelOpening((e) => {
            this.onSshChannelOpening(e, session);
        });
        const closedEventRegistration = session.onClosed((e) => {
            this.session_Closed(e, cancellation);
        });

        try {
            const nodeStream = new NodeStream(stream);
            await session.connect(nodeStream);
            this.sshSessions.push(session);
            await tcs.promise;
        } finally {
            authenticatingEventRegistration.dispose();
            channelOpeningEventRegistration.dispose();
            closedEventRegistration.dispose();
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
        if (this.tunnel && this.tunnel.ports) {
            this.tunnel.ports.forEach(async (port) => {
                try {
                    await this.forwardPort(pfs, port);
                } catch (ex) {
                    this.trace(
                        TraceLevel.Error,
                        0,
                        `Error forwarding port ${port.portNumber}: ${ex}`,
                    );
                }
            });
        }
    }

    private onSshChannelOpening(e: SshChannelOpeningEventArgs, session: any) {
        if (!(e.request instanceof PortForwardChannelOpenMessage)) {
            // This is to let the Go SDK open an unused session channel
            if (e.request.channelType === 'session') {
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
            if (!(session instanceof SshServerSession)) {
                this.trace(TraceLevel.Warning, 0, 'Rejecting request due to invalid sender');
                e.failureReason = SshChannelOpenFailureReason.connectFailed;
            } else {
                let sessionId = (session as SshServerSession).sessionId;
                if (!sessionId) {
                    this.trace(TraceLevel.Warning, 0, 'Rejecting request as session has no Id');
                    e.failureReason = SshChannelOpenFailureReason.administrativelyProhibited;
                    return;
                }

                if (
                    !this.remoteForwarders[
                        new SessionPortKey(sessionId, portForwardRequest.port).toString()
                    ]
                ) {
                    this.trace(
                        TraceLevel.Warning,
                        0,
                        'Rejecting request to connect to non-forwarded port:' +
                            portForwardRequest.port,
                    );
                    e.failureReason = SshChannelOpenFailureReason.administrativelyProhibited;
                }
            }
        } else {
            this.trace(
                TraceLevel.Warning,
                0,
                'Nonrecognized channel type ' + portForwardRequest.channelType,
            );
            e.failureReason = SshChannelOpenFailureReason.unknownChannelType;
        }
    }

    private session_Closed(e: SshSessionClosedEventArgs, cancellation: CancellationToken) {
        if (e.reason === SshDisconnectReason.byApplication) {
            this.trace(TraceLevel.Info, 0, 'Client ssh session closed.');
        } else if (cancellation.isCancellationRequested) {
            this.trace(TraceLevel.Info, 0, 'Client ssh session cancelled.');
        } else {
            this.trace(
                TraceLevel.Error,
                0,
                `Client ssh session closed unexpectely due to ${e.reason}, \"${e.message}\"\n${e.error}`,
            );
        }
    }

    private hostSession_Closed(
        channelOpenEventRegistration: Disposable,
        closeEventRegistration: Disposable,
    ) {
        closeEventRegistration.dispose();
        channelOpenEventRegistration.dispose();
        this.hostSession = undefined;
        this.trace(TraceLevel.Info, 0, 'Connection to host tunnel relay closed.');
    }

    public async dispose(): Promise<void> {
        this.disposeCts.cancel();

        let hostSession = this.hostSession;
        if (hostSession) {
            this.hostSession = undefined;
            try {
                await hostSession.close();
            } catch (e) {
                if (!(e instanceof ObjectDisposedError)) throw e;
            }
        }

        let promises: Promise<any>[] = Object.assign([], this.clientSessionPromises);
        this.clientSessionPromises.length = 0;

        if (this.tunnel) {
            const promise = this.managementClient.deleteTunnelEndpoints(
                this.tunnel,
                this.hostId,
                TunnelConnectionMode.TunnelRelay,
            );
            promises.push(promise);
        }

        for (const key of Object.keys(this.remoteForwarders)) {
            this.remoteForwarders[key].dispose();
        }

        this.sshSessions.forEach((sshSession) => {
            promises.push(sshSession.close(SshDisconnectReason.byApplication));
        });

        await Promise.all(promises);
        this.clientSessionPromises.length = 0;
    }
}
