// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelAccessScopes } from '@microsoft/dev-tunnels-contracts';
import {
    TunnelAccessTokenProperties,
    TunnelManagementClient,
    TunnelRequestOptions,
} from '@microsoft/dev-tunnels-management';
import { CancellationError, Stream, Trace, TraceLevel } from '@microsoft/dev-tunnels-ssh';
import { CancellationToken } from 'vscode-jsonrpc';
import { ConnectionStatus } from './connectionStatus';
import { RelayTunnelConnector } from './relayTunnelConnector';
import { TunnelConnector } from './tunnelConnector';
import { TunnelSession } from './tunnelSession';
import { withCancellation } from './utils';
import { TunnelConnectionBase } from './tunnelConnectionBase';
import {
    PortForwardChannelOpenMessage,
    PortForwardRequestMessage,
    PortForwardSuccessMessage,
} from '@microsoft/dev-tunnels-ssh-tcp';
import { PortRelayRequestMessage } from './messages/portRelayRequestMessage';
import { PortRelayConnectRequestMessage } from './messages/portRelayConnectRequestMessage';
import * as http from 'http';

/**
 * Tunnel connection session.
 */
export class TunnelConnectionSession extends TunnelConnectionBase implements TunnelSession {
    private connectedTunnel: Tunnel | null = null;
    private connector?: TunnelConnector;
    private reconnectPromise?: Promise<void>;
    private connectionProtocolValue?: string;

    public httpAgent?: http.Agent;

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
     * Tunnel access token.
     */
    protected accessToken?: string;

    public constructor(
        tunnelAccessScope: string,
        trace?: Trace,
        /**
         * Gets the management client used for the connection.
         */
        protected readonly managementClient?: TunnelManagementClient,
    ) {
        super(tunnelAccessScope);
        this.trace = trace ?? (() => {});
        this.httpAgent = managementClient?.httpsAgent;
    }

    /**
     * Gets the trace source.
     */
    public trace: Trace;

    /**
     * Get the tunnel of this tunnel connection.
     */
    public get tunnel(): Tunnel | null {
        return this.connectedTunnel;
    }

    private set tunnel(value: Tunnel | null) {
        if (value !== this.connectedTunnel) {

            // Get the tunnel access token from the new tunnel, or the original Tunnal object if the new tunnel doesn't have the token,
            // which may happen when the tunnel was authenticated with a tunnel access token from Tunnel.AccessTokens.
            // Add the tunnel access token to the new tunnel's AccessTokens if it is not there.
            if (value &&
                !TunnelAccessTokenProperties.getTunnelAccessToken(value, this.tunnelAccessScope)) {

                const accessToken = TunnelAccessTokenProperties.getTunnelAccessToken(
                    this.tunnel,
                    this.tunnelAccessScope,
                );

                if (accessToken) {
                    value.accessTokens ??= {};
                    value.accessTokens[this.tunnelAccessScope] = accessToken;
                }
            }

            this.connectedTunnel = value;
            this.tunnelChanged();
        }
    }

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
     * Creates a stream to the tunnel.
     */
    public createSessionStream(
        cancellation: CancellationToken,
    ): Promise<{ stream: Stream, protocol: string }> {
        throw new Error('Not implemented');
    }

    /**
     * Configures the tunnel session with the given stream.
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
     * Closes the tunnel session due to an error.
     */
    public closeSession(error?: Error): Promise<void> {
        this.disconnectError = error;
        return Promise.resolve();
    }

    /**
     * Refreshes the tunnel access token. This may be useful when the Relay service responds with 401 Unauthorized.
     * Does nothing if the object is disposed, or there is no way to refresh the token.
     */
    public async refreshTunnelAccessToken(cancellation: CancellationToken): Promise<boolean> {
        if (this.isDisposed) {
            return false;
        }

        if (!this.isRefreshingTunnelAccessTokenEventHandled && !this.canRefreshTunnel) {
            return false;
        }

        const previousStatus = this.connectionStatus;
        this.connectionStatus = ConnectionStatus.RefreshingTunnelAccessToken;
        try {
            this.traceInfo(
                `Refreshing tunnel access token. Current token: ${TunnelAccessTokenProperties.getTokenTrace(
                    this.accessToken,
                )}`,
            );

            if (this.isRefreshingTunnelAccessTokenEventHandled) {
                this.accessToken = await this.getFreshTunnelAccessToken(cancellation) ?? undefined;
            } else {
                await this.refreshTunnel(cancellation);
            }

            if (this.accessToken) {
                TunnelAccessTokenProperties.validateTokenExpiration(this.accessToken);
            }

            this.traceInfo(
                `Refreshed tunnel access token. New token: ${TunnelAccessTokenProperties.getTokenTrace(
                    this.accessToken,
                )}`,
            );

            return true;
        } finally {
            this.connectionStatus = previousStatus;
        }

        return false;
    }

    /**
     * Get a value indicating whether this session can attempt refreshing tunnel.
     * Note: tunnel refresh may still fail if the tunnel doesn't exist in the service, 
     * tunnel access has changed, or tunnel access token has expired.
     */
    protected get canRefreshTunnel() {
        return this.tunnel && this.managementClient;
    }

    /**
     * Fetch the tunnel from the service if {@link managementClient} and {@link tunnel} are set.
     */
    protected async refreshTunnel(cancellation?: CancellationToken) {
        if (this.canRefreshTunnel) {
            this.traceInfo('Refreshing tunnel.');
            const options: TunnelRequestOptions = {
                tokenScopes: [this.tunnelAccessScope],
            };

            this.tunnel = await withCancellation(
                this.managementClient!.getTunnel(this.tunnel!, options),
                cancellation,
            );

            if (this.tunnel) {
                this.traceInfo('Refreshed tunnel.');
            } else {
                this.traceInfo('Tunnel not found.');
            }
        }
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
     * Start reconnecting if the tunnel connection is not yet disposed.
     */
    protected startReconnectingIfNotDisposed() {
        if (!this.isDisposed && !this.reconnectPromise) {
            this.reconnectPromise = (async () => {
                try {
                    await this.connectTunnelSession();
                } catch {
                    // Tracing of the error has already been done by connectTunnelSession.
                    // As reconnection is an async process, there is nobody watching it throw.
                    // The error, if it was not cancellation, is stored in disconnectError property.
                    // There might have been connectionStatusChanged event fired as well.
                }
                this.reconnectPromise = undefined;
            })();
        }
    }

    /**
     * Connect to the tunnel session by running the provided {@link action}.
     */
    public async connectSession(action: () => Promise<void>): Promise<void> {
        this.connectionStatus = ConnectionStatus.Connecting;
        try {
            await action();
            this.connectionStatus = ConnectionStatus.Connected;
        } catch (e) {
            if (!(e instanceof CancellationError)) {
                const name =
                    this.tunnelAccessScope === TunnelAccessScopes.Connect ? 'client' : 'host';
                if (e instanceof Error) {
                    this.traceError(`Error connecting ${name} tunnel session: ${e.message}`, e);
                    this.disconnectError = e;
                } else {
                    const message = `Error connecting ${name} tunnel session: ${e}`;
                    this.traceError(message);
                    this.disconnectError = new Error(message);
                }
            }
            this.connectionStatus = ConnectionStatus.Disconnected;
            throw e;
        }
    }

    /**
     * Connect to the tunnel session with the tunnel connector.
     * @param tunnel Tunnel to use for the connection.
     *     Undefined if the connection information is already known and the tunnel is not needed.
     *     Tunnel object to get the connection information from that tunnel.
     */
    public async connectTunnelSession(tunnel?: Tunnel, httpAgent?: http.Agent): Promise<void> {
        if (tunnel) {
            if (this.tunnel) {
                throw new Error(
                    'Already connected to a tunnel. Use separate instances to connect to multiple tunnels.',
                );
            }
            
            this.tunnel = tunnel;
        }

        this.httpAgent ??= httpAgent;

        await this.connectSession(async () => {
            const isReconnect = this.isReconnectable && !tunnel;
            await this.onConnectingToTunnel();
            if (!this.connector) {
                this.connector = this.createTunnelConnector();
            }
            await this.connector.connectSession(isReconnect, this.disposeToken);
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
}
