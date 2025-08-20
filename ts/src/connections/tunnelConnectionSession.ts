// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    Tunnel,
    TunnelAccessScopes,
    TunnelProgress,
    TunnelReportProgressEventArgs,
    TunnelEvent,
} from '@microsoft/dev-tunnels-contracts';
import {
    TunnelAccessTokenProperties,
    TunnelManagementClient,
    TunnelRequestOptions,
} from '@microsoft/dev-tunnels-management';
import {
    CancellationError,
    ObjectDisposedError,
    Progress,
    SshClientSession,
    SshDisconnectReason,
    SshSession,
    SshSessionClosedEventArgs,
    Stream,
    Trace,
    TraceLevel
} from '@microsoft/dev-tunnels-ssh';
import { CancellationToken, CancellationTokenSource, Disposable, Emitter, Event } from 'vscode-jsonrpc';
import { ConnectionStatus } from './connectionStatus';
import { RelayTunnelConnector } from './relayTunnelConnector';
import { TunnelConnector } from './tunnelConnector';
import { TunnelSession } from './tunnelSession';
import { TrackingEmitter, withCancellation } from './utils';
import { TunnelConnectionBase } from './tunnelConnectionBase';
import {
    PortForwardChannelOpenMessage,
    PortForwardRequestMessage,
    PortForwardSuccessMessage,
} from '@microsoft/dev-tunnels-ssh-tcp';
import { PortRelayRequestMessage } from './messages/portRelayRequestMessage';
import { PortRelayConnectRequestMessage } from './messages/portRelayConnectRequestMessage';
import * as http from 'http';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import { RefreshingTunnelEventArgs } from './refreshingTunnelEventArgs';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { TunnelRelayStreamFactory } from './tunnelRelayStreamFactory';
import { DefaultTunnelRelayStreamFactory } from './defaultTunnelRelayStreamFactory';
import { IClientConfig } from 'websocket';
import { v4 as uuidv4 } from 'uuid';

/**
 * Tunnel connection session.
 */
export class TunnelConnectionSession extends TunnelConnectionBase implements TunnelSession {
    protected connectionOptions?: TunnelConnectionOptions;
    private connectedTunnel: Tunnel | null = null;
    private connector?: TunnelConnector;
    private reconnectPromise?: Promise<void>;
    private connectionProtocolValue?: string;
    private disconnectionReason?: SshDisconnectReason;
    private connectionStartTime: number = Date.now();

    private readonly uniqueConnectionId: string = uuidv4();

    private readonly refreshingTunnelEmitter =
        new TrackingEmitter<RefreshingTunnelEventArgs>();

    private readonly reportProgressEmitter = new Emitter<TunnelReportProgressEventArgs>();

    /**
     * Event that is raised to report connection progress.
     *
     * See `Progress` for a description of the different progress events that can be reported.
     */
    public readonly onReportProgress: Event<TunnelReportProgressEventArgs> = this.reportProgressEmitter.event;

    public httpAgent?: http.Agent;

    /**
     * Tunnel relay URI.
     * @internal
     */
    public relayUri?: string;

    /**
     * Gets or sets a factory for creating relay streams.
     */
    public streamFactory: TunnelRelayStreamFactory = new DefaultTunnelRelayStreamFactory();

    /**
     * Name of the protocol used to connect to the tunnel.
     */
    public get connectionProtocol(): string | undefined {
        return this.connectionProtocolValue;
    }
    protected set connectionProtocol(value: string | undefined) {
        this.connectionProtocolValue = value;
    }

    /**
     * Gets an ID that is unique to this instance of `TunnelConnectionSession`,
     * useful for correlating connection events over time.
     */
    protected get connectionId() {
        return this.uniqueConnectionId;
    }

    /**
     * A value indicating if this is a client tunnel connection (as opposed to host connection).
     */
    protected get isClientConnection(): boolean {
        return this.tunnelAccessScope === TunnelAccessScopes.Connect;
    }

    /**
     * tunnel connection role, either "client", or "host", depending on @link tunnelAccessScope.
     */
    protected get connectionRole(): string {
        return this.isClientConnection ? 'client' : 'host';
    }

    /**
     * @internal onRetrying override to report tunnel events.
     */
    public onRetrying(event: RetryingTunnelConnectionEventArgs): void {
        // Report tunnel event for retry
        if (this.tunnel && this.managementClient) {
            const retryingEvent: TunnelEvent = {
                name: `${this.connectionRole}_connect_retrying`,
                severity: TunnelEvent.warning,
                details: event.error?.toString(),
                properties: {
                    'Retry': event.retry.toString(),
                    'Delay': event.delayMs.toString()
                },
            };
            this.managementClient.reportEvent(this.tunnel, retryingEvent);
        }

        super.onRetrying(event);
    }

    /**
     * @internal onConnectionStatusChanged override to report tunnel events.
     */
    protected onConnectionStatusChanged(
        previousStatus: ConnectionStatus,
        status: ConnectionStatus,
    ) {
        // Report tunnel event for connection status change
        if (this.tunnel && this.managementClient) {
            const statusEvent: TunnelEvent = {
                name: `${this.connectionRole}_connection_status`,
                severity: TunnelEvent.info,
                details: undefined,
                properties: {
                    ConnectionStatus: status.toString(),
                    PreviousConnectionStatus: previousStatus.toString()
                },
            };

            if (previousStatus !== ConnectionStatus.None) {
                // Format the duration as a TimeSpan in the form "00:00:00.000"
                const duration = Date.now() - this.connectionStartTime;
                const formattedDuration = new Date(duration).toISOString().substring(11, 23);
                statusEvent.properties![`${previousStatus}Duration`] = formattedDuration;
            }

            if (this.isClientConnection) {
                // For client sessions, report the SSH session ID property, which is derived from
                // the SSH key-exchange such that both host and client have the same ID.
                statusEvent.properties!.ClientSessionId = this.getShortSessionId(this.sshSession);
            } else {
                // For host sessions, there is no SSH encryption or key-exchange.
                // Just use a locally-generated GUID that is unique to this session.
                statusEvent.properties!.HostSessionId = this.connectionId;
            }

            this.managementClient.reportEvent(this.tunnel, statusEvent);
        }

        this.connectionStartTime = Date.now();
        super.onConnectionStatusChanged(previousStatus, status);
    }

    /**
     * Tunnel access token.
     */
    protected accessToken?: string;

    /**
     * SSH session that is used to connect to the tunnel.
     * @internal
     */
    protected sshSession?: SshClientSession;
    protected sshSessionDisposables: Disposable[] = [];

    public constructor(
        tunnelAccessScope: string,
        protected readonly connectionProtocols: string[],
        /**
         * Gets the management client used for the connection.
         */
        protected readonly managementClient?: TunnelManagementClient,
        trace?: Trace,
    ) {
        super(tunnelAccessScope);
        this.trace = trace ?? (() => {});
        this.httpAgent = managementClient?.httpsAgent;
    }

    /**
     * Gets the trace source.
     */
    public trace: Trace;

    /* @internal */
    public raiseReportProgress(progress: Progress|TunnelProgress, sessionNumber?: number) {
        const args : TunnelReportProgressEventArgs  = {
            progress,
            sessionNumber,
        };
        this.reportProgressEmitter.fire(args);
    }

    /**
     * Get the tunnel of this tunnel connection.
     */
    public get tunnel(): Tunnel | null {
        return this.connectedTunnel;
    }

    private set tunnel(value: Tunnel | null) {
        if (value !== this.connectedTunnel) {
            this.connectedTunnel = value;
            this.tunnelChanged();
        }
    }

    /**
     * An event which fires when tunnel connection refreshes tunnel.
     */
    public readonly refreshingTunnel = this.refreshingTunnelEmitter.event;

    /**
     * Tunnel has been assigned to or changed.
     */
    protected tunnelChanged() {
        if (this.tunnel) {
            this.accessToken = TunnelAccessTokenProperties.getTunnelAccessToken(this.tunnel, this.tunnelAccessScope);
        } else {
            this.accessToken = undefined;
        }
    }

    /**
     * Determines whether E2E encryption is requested when opening connections through the tunnel
     * (V2 protocol only).
     *
     * The default value is true, but applications may set this to false (for slightly faster
     * connections).
     *
     * Note when this is true, E2E encryption is not strictly required. The tunnel relay and
     * tunnel host can decide whether or not to enable E2E encryption for each connection,
     * depending on policies and capabilities. Applications can verify the status of E2EE by
     * handling the `forwardedPortConnecting` event and checking the related property on the
     * channel request or response message.
     */
    public enableE2EEncryption: boolean = true;

    /**
     * Gets a value indicating that this connection has already created its connector
     * and so can be reconnected if needed.
     */
    protected get isReconnectable(): boolean {
        return !!this.connector;
    }

    /**
     * Gets the disconnection reason.
     * {@link SshDisconnectReason.none } if not yet disconnected.
     * {@link SshDisconnectReason.connectionLost} if network connection was lost and reconnects are not enabled or unsuccesfull.
     * {@link SshDisconnectReason.byApplication} if connection was disposed.
     * {@link SshDisconnectReason.tooManyConnections} if host connection was disconnected because another host connected for the same tunnel.
     */
    public get disconnectReason(): SshDisconnectReason | undefined {
        return this.disconnectionReason;
    }

    /**
     * Sets the disconnect reason that caused disconnection.
     */
    protected set disconnectReason(reason: SshDisconnectReason | undefined) {
        this.disconnectionReason = reason;
    }

    /**
     * @internal Creates a stream to the tunnel.
     */
    public async createSessionStream(
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<{ stream: Stream, protocol: string }> {
        if (!this.relayUri) {
            throw new Error(
                'Cannot create tunnel session stream. Tunnel relay endpoint URI is missing',
            );
        }

        if (this.isClientConnection) {
            this.raiseReportProgress(Progress.OpeningClientConnectionToRelay);
        } else {
            this.raiseReportProgress(Progress.OpeningHostConnectionToRelay);
        }

        this.trace(TraceLevel.Info, 0, `Connecting to ${this.connectionRole} tunnel relay ${this.relayUri}`);
        this.trace(TraceLevel.Verbose, 0, `Sec-WebSocket-Protocol: ${this.connectionProtocols.join(', ')}`);
        if (this.accessToken) {
            const tokenTrace = TunnelAccessTokenProperties.getTokenTrace(this.accessToken);
            this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel <${tokenTrace}>`);
        }

        const clientConfig: IClientConfig = {
            tlsOptions: {
                agent: this.httpAgent,
            },
        };

        const streamAndProtocol = await this.streamFactory.createRelayStream(
            this.relayUri,
            this.connectionProtocols,
            this.accessToken,
            clientConfig
        );

        this.trace(
            TraceLevel.Verbose,
            0,
            `Connected with subprotocol '${streamAndProtocol.protocol}'`);
        if (this.isClientConnection) {
            this.raiseReportProgress(Progress.OpenedClientConnectionToRelay);
        } else {
            this.raiseReportProgress(Progress.OpenedHostConnectionToRelay);
        }
        return streamAndProtocol;
    }

    /**
     * @internal Configures the tunnel session with the given stream.
     */
    public configureSession(
        stream: Stream,
        protocol: string,
        isReconnect: boolean,
        cancellation: CancellationToken,
    ): Promise<void> {
        throw new Error('Not implemented');
    }

    /**
     * @internal Closes the tunnel session due to an error.
     */
    public async closeSession(reason?: SshDisconnectReason, error?: Error): Promise<void> {
        this.unsubscribeSessionEvents();

        const session = this.sshSession;
        if (!session) {
            return;
        }

        if (!session.isClosed) {
            await session.close(reason || SshDisconnectReason.none, undefined, error);
        } else {
            this.sshSession = undefined;
        }

        // Closing the SSH session does nothing if the session is in disconnected state,
        // which may happen for a reconnectable session when the connection drops.
        // Disposing of the session forces closing and frees up the resources.
        session.dispose();
    }

    /**
     * Disposes this tunnel session, closing the SSH session used for it.
     */
    public async dispose(): Promise<void> {
        if (this.disconnectReason === SshDisconnectReason.none ||
            this.disconnectReason === undefined) {
            this.disconnectReason = SshDisconnectReason.byApplication;
        }

        await super.dispose();
        try {
            await this.closeSession(this.disconnectReason, this.disconnectError);
        } catch (e) {
            if (!(e instanceof ObjectDisposedError)) throw e;
        }
    }

    /**
     * Refreshes the tunnel access token. This may be useful when the Relay service responds with 401 Unauthorized.
     * Does nothing if the object is disposed, or there is no way to refresh the token.
     * @internal
     */
    public async refreshTunnelAccessToken(cancellation: CancellationToken): Promise<boolean> {
        if (this.isDisposed) {
            return false;
        }

        if (!this.isRefreshingTunnelAccessTokenEventHandled && !this.canRefreshTunnel) {
            return false;
        }

        this.connectionStatus = ConnectionStatus.RefreshingTunnelAccessToken;
        try {
            this.traceVerbose(
                `Refreshing tunnel access token. Current token: ${TunnelAccessTokenProperties.getTokenTrace(
                    this.accessToken,
                )}`,
            );

            if (this.isRefreshingTunnelAccessTokenEventHandled) {
                this.accessToken = await this.getFreshTunnelAccessToken(cancellation) ?? undefined;
            } else {
                await this.refreshTunnel(false, cancellation);
            }

            this.traceVerbose(
                `Refreshed tunnel access token. New token: ${TunnelAccessTokenProperties.getTokenTrace(
                    this.accessToken,
                )}`,
            );

            return true;
        } finally {
            this.connectionStatus = ConnectionStatus.Connecting;
        }
    }

    /**
     * @internal Start connecting relay client.
     */
    public startConnecting(): void {
        this.connectionStatus = ConnectionStatus.Connecting;
    }

    /**
     * @internal Finish connecting relay client.
     */
    public finishConnecting(reason?: SshDisconnectReason, disconnectError?: Error): void {
        if (reason === undefined || reason === SshDisconnectReason.none) {
            if (this.connectionStatus === ConnectionStatus.Connecting) {
                // If there were temporary connection issue, disconnectError may contain the old error.
                // Since we have successfully connected after all, clean it up.
                this.disconnectError = undefined;
                this.disconnectReason = undefined;
            }

            this.connectionStatus = ConnectionStatus.Connected;
        } else if (this.connectionStatus !== ConnectionStatus.Disconnected) {
            // Do not overwrite disconnect error and reason if already disconnected.
            this.disconnectReason = reason;
            if (disconnectError) {
                this.disconnectError = disconnectError;
            }

            this.connectionStatus = ConnectionStatus.Disconnected;
        }
    }

    /**
     * Get a value indicating whether this session can attempt refreshing tunnel.
     * Note: tunnel refresh may still fail if the tunnel doesn't exist in the service, 
     * tunnel access has changed, or tunnel access token has expired.
     */
    protected get canRefreshTunnel() {
        return (this.tunnel && this.managementClient) || this.refreshingTunnelEmitter.isSubscribed;
    }

    /**
     * Fetch the tunnel from the service if {@link managementClient} and {@link tunnel} are set.
     */
    protected async refreshTunnel(
        includePorts?: boolean,
        cancellation?: CancellationToken
        ): Promise<boolean> {

        this.traceInfo('Refreshing tunnel.');
        let isRefreshed = false;

        const e = new RefreshingTunnelEventArgs(this.tunnelAccessScope, this.tunnel, !!includePorts, this.managementClient, cancellation);
        this.refreshingTunnelEmitter.fire(e);
        if (e.tunnelPromise) {
            this.tunnel = await e.tunnelPromise;
            isRefreshed = true;
        }

        if (!isRefreshed && this.tunnel && this.managementClient) {
            const options: TunnelRequestOptions = {
                tokenScopes: [this.tunnelAccessScope],
                includePorts,
            };
    
            this.tunnel = await withCancellation(
                this.managementClient!.getTunnel(this.tunnel!, options),
                cancellation,
            );
    
            isRefreshed = true;
        }
        
        if (isRefreshed) {
            if (this.tunnel) {
                this.traceInfo('Refreshed tunnel.');
            } else {
                this.traceInfo('Tunnel not found.');
            }
        }

        return true;
    }

    /**
     * Creates a tunnel connector
     */
    protected createTunnelConnector(): TunnelConnector {
        return new RelayTunnelConnector(this);
    }

    /**
     * Trace info message.
     */
    protected traceInfo(msg: string) {
        this.trace(TraceLevel.Info, 0, msg);
    }

    /**
     * Trace verbose message.
     */
    protected traceVerbose(msg: string) {
        this.trace(TraceLevel.Verbose, 0, msg);
    }

    /**
     * Trace warning message.
     */
    protected traceWarning(msg: string, err?: Error) {
        this.trace(TraceLevel.Warning, 0, msg, err);
    }

    /**
     * Trace error message.
     */
    protected traceError(msg: string, err?: Error) {
        this.trace(TraceLevel.Error, 0, msg, err);
    }

    /**
     * SSH session closed event handler. Child classes may use it unsubscribe session events and maybe start reconnecting.
     */
    protected onSshSessionClosed(e: SshSessionClosedEventArgs) {
        this.unsubscribeSessionEvents();
        this.sshSession = undefined;
        this.maybeStartReconnecting(e.reason, e.message, e.error);
    }

    /**
     * Start reconnecting if the tunnel connection is not yet disposed.
     */
    protected maybeStartReconnecting(reason?: SshDisconnectReason, message?: string, error?: Error|null) {
        const traceMessage = `Connection to ${this.connectionRole} tunnel relay closed.${this.getDisconnectReason(reason, message, error)}`;
        if (this.isDisposed || this.connectionStatus === ConnectionStatus.Disconnected) {
            // Disposed or disconnected already.
            // This reconnection attempt may be caused by closing SSH session on dispose.
            this.traceInfo(traceMessage);
            return;
        }

        if (error) {
            this.disconnectError = error;
            this.disconnectReason = reason;
        }

        if (this.connectionStatus !== ConnectionStatus.Connected || this.reconnectPromise) {
            // Not connected or already connecting.
            this.traceInfo(traceMessage);
            return;
        }

        // Reconnect if connection is lost, reconnect is enabled, and connector exists.
        // The connector may be undefined if the tunnel client/host was created directly from a stream.
        if ((this.connectionOptions?.enableReconnect ?? true) &&
            reason === SshDisconnectReason.connectionLost &&
            this.connector) {

            // Report reconnect event
            if (this.tunnel && this.managementClient) {
                const reconnectEvent: TunnelEvent = {
                    name: `${this.connectionRole}_reconnect`,
                    severity: TunnelEvent.warning,
                    details: error?.toString() ?? message,
                    properties: {
                        ClientSessionId: this.getShortSessionId(this.sshSession),
                    },
                };
                this.managementClient.reportEvent(this.tunnel, reconnectEvent);
            }

            this.traceInfo(`${traceMessage} Reconnecting.`);
            this.reconnectPromise = (async () => {
                try {
                    await this.connectTunnelSession();
                } catch (ex) {
                    // Report reconnect failed event
                    if (this.tunnel && this.managementClient) {
                        const reconnectFailedEvent: TunnelEvent = {
                            name: `${this.connectionRole}_reconnect_failed`,
                            severity: TunnelEvent.error,
                            details: ex instanceof Error ? ex.toString() : String(ex),
                            properties: {
                                ClientSessionId: this.getShortSessionId(this.sshSession),
                            },
                        };
                        this.managementClient.reportEvent(this.tunnel, reconnectFailedEvent);
                    }
                    // Tracing of the error has already been done by connectTunnelSession.
                    // As reconnection is an async process, there is nobody watching it throw.
                    // The error, if it was not cancellation, is stored in disconnectError property.
                    // There might have been connectionStatusChanged event fired as well.
                }
                this.reconnectPromise = undefined;
            })();
        } else {
            // Report disconnect event
            if (this.tunnel && this.managementClient) {
                const disconnectEvent: TunnelEvent = {
                    name: `${this.connectionRole}_disconnect`,
                    severity: TunnelEvent.warning,
                    details: error?.toString() ?? message,
                    properties: {
                        ClientSessionId: this.getShortSessionId(this.sshSession),
                    },
                };
                this.managementClient.reportEvent(this.tunnel, disconnectEvent);
            }

            this.traceInfo(traceMessage);
            this.connectionStatus = ConnectionStatus.Disconnected;
        }
    }

    /**
     * Get a user-readable reason for SSH session disconnection, or an empty string.
     */
    protected getDisconnectReason(reason?: SshDisconnectReason, message?: string, error?: Error|null): string {
        switch (reason) {
            case SshDisconnectReason.connectionLost:
                return ` ${message || error?.message || 'Connection lost.'}`;
            case SshDisconnectReason.authCancelledByUser:
            case SshDisconnectReason.noMoreAuthMethodsAvailable:
            case SshDisconnectReason.hostNotAllowedToConnect:
            case SshDisconnectReason.illegalUserName:
                return ' Not authorized.';
            case SshDisconnectReason.serviceNotAvailable:
                return ' Service not available.';
            case SshDisconnectReason.compressionError:
            case SshDisconnectReason.keyExchangeFailed:
            case SshDisconnectReason.macError:
            case SshDisconnectReason.protocolError:
                return ' Protocol error.';
            case SshDisconnectReason.tooManyConnections:
                return this.isClientConnection ? ' Too many client connections.' : ' Another host for the tunnel has connected.';
            default:
                return '';
        }
    }

    /**
     * Connect to the tunnel session by running the provided {@link action}.
     */
    public async connectSession(action: () => Promise<void>): Promise<void> {
        try {
            await action();
        } catch (e) {
            if (!(e instanceof CancellationError)) {
                if (e instanceof Error) {
                    this.traceError(`Error connecting ${this.connectionRole} tunnel session: ${e.message}`, e);
                } else {
                    const message = `Error connecting ${this.connectionRole} tunnel session: ${e}`;
                    this.traceError(message);
                }

                if (this.tunnel && this.managementClient) {
                    const connectFailedEvent: TunnelEvent = {
                        name: `${this.connectionRole}_connect_failed`,
                        severity: TunnelEvent.error,
                        details: e instanceof Error ? e.toString() : String(e),
                    };
                    this.managementClient.reportEvent(this.tunnel, connectFailedEvent);
                }
            }
            throw e;
        }
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
        cancellation?: CancellationToken): Promise<void> {
        if (tunnel) {
            this.tunnel = tunnel;
        }
        if (options) {
            this.connectionOptions = options;
            this.httpAgent ??= options?.httpAgent;
        }

        await this.connectSession(async () => {
            const isReconnect = this.isReconnectable && !tunnel;
            await this.onConnectingToTunnel();
            if (!this.connector) {
                this.connector = this.createTunnelConnector();
            }

            const disposables: Disposable[] = [];
            if (cancellation) {
                // Link the provided cancellation token with the dispose token.
                const linkedCancellationSource = new CancellationTokenSource();
                disposables.push(
                    linkedCancellationSource,
                    cancellation.onCancellationRequested(() => linkedCancellationSource!.cancel()),
                    this.disposeToken.onCancellationRequested(() => linkedCancellationSource!.cancel()),
                );
                cancellation = linkedCancellationSource.token;
            } else {
                cancellation = this.disposeToken;
            }
            
            try {
                await this.connector.connectSession(isReconnect, options, cancellation);
            } catch (e) {
                if (e instanceof CancellationError) {
                    this.throwIfDisposed(`CancelationError: ${e.message}`, e.stack);
                }
                throw e;
            } finally {
                for (const disposable of disposables) disposable.dispose();
            }
        });
    }

    /**
     * Validate the {@link tunnel} and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     */
    public onConnectingToTunnel(): Promise<void> {
        return Promise.resolve();
    }

    /**
     * Validates tunnel access token if it's present. Returns the token.
     * Note: uses client's system time for the validation.
     */
    public validateAccessToken(): string | undefined {
        if (this.accessToken) {
            TunnelAccessTokenProperties.validateTokenExpiration(this.accessToken);
            return this.accessToken;
        }
    }

    /** @internal */
    public createRequestMessageAsync(port: number): Promise<PortForwardRequestMessage> {
        const message = new PortRelayRequestMessage();
        message.accessToken = this.accessToken;
        return Promise.resolve(message);
    }

    /** @internal */
    public createSuccessMessageAsync(port: number): Promise<PortForwardSuccessMessage> {
        const message = new PortForwardSuccessMessage();
        return Promise.resolve(message);
    }

    /** @internal */
    public createChannelOpenMessageAsync(port: number): Promise<PortForwardChannelOpenMessage> {
        const message = new PortRelayConnectRequestMessage();
        message.accessToken = this.accessToken;
        message.isE2EEncryptionRequested = this.enableE2EEncryption;
        return Promise.resolve(message);
    }

    /**
     * Unsubscribe SSH session events in @link TunnelSshConnectionSession.sshSessionDisposables
     */
    protected unsubscribeSessionEvents() {
        this.sshSessionDisposables.forEach((d) => d.dispose());
        this.sshSessionDisposables = [];
    }

    /** @internal */
    protected getShortSessionId(session?: SshSession): string {
        const b = session?.sessionId;
        if (!b || b.length < 16) {
            return '';
        }

        // Format as a GUID. This cannot use uuid.stringify() because
        // the bytes might not be technically valid for a UUID.
        return b.subarray(0, 4).toString('hex') + '-' +
            b.subarray(4, 6).toString('hex') + '-' +
            b.subarray(6, 8).toString('hex') + '-' +
            b.subarray(8, 10).toString('hex') + '-' +
            b.subarray(10, 16).toString('hex');
    }
}
