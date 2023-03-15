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

/**
 * Tunnel connection session.
 */
export class TunnelConnectionSession extends TunnelConnectionBase implements TunnelSession {
    private connectedTunnel: Tunnel | null = null;
    private connector?: TunnelConnector;
    private reconnectPromise?: Promise<void>;

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
        this.connectedTunnel = value;
    }

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
    public createSessionStream(cancellation: CancellationToken): Promise<Stream> {
        throw new Error('Not implemented');
    }

    /**
     * Configures the tunnel session with the given stream.
     */
    public configureSession(
        stream: Stream,
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
     */
    public async refreshTunnelAccessToken(cancellation: CancellationToken): Promise<boolean> {
        if (this.isDisposed) {
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
            const newAccessToken = await this.getFreshTunnelAccessToken(cancellation);
            if (newAccessToken) {
                TunnelAccessTokenProperties.validateTokenExpiration(newAccessToken);
                this.traceInfo(
                    `Refreshed tunnel access token. New token: ${TunnelAccessTokenProperties.getTokenTrace(
                        newAccessToken,
                    )}`,
                );
                this.accessToken = newAccessToken;
                return true;
            }
        } finally {
            this.connectionStatus = previousStatus;
        }

        return false;
    }

    /**
     * Gets the fresh tunnel access token or undefined if it cannot.
     */
    protected async getFreshTunnelAccessToken(
        cancellation: CancellationToken,
    ): Promise<string | null | undefined> {
        if (!this.isRefreshingTunnelAccessTokenEventHandled) {
            if (!this.tunnel || !this.managementClient) {
                return;
            }
            const options: TunnelRequestOptions = {
                tokenScopes: [this.tunnelAccessScope],
            };
            this.tunnel = await withCancellation(
                this.managementClient.getTunnel(this.tunnel, options),
                cancellation,
            );
            if (!this.tunnel?.accessTokens) {
                return;
            }
            return TunnelAccessTokenProperties.getTunnelAccessToken(
                this.tunnel,
                this.tunnelAccessScope,
            );
        }

        return await super.getFreshTunnelAccessToken(cancellation);
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
    public async connectTunnelSession(tunnel?: Tunnel): Promise<void> {
        if (tunnel && this.tunnel) {
            throw new Error(
                'Already connected to a tunnel. Use separate instances to connect to multiple tunnels.',
            );
        }
        await this.connectSession(async () => {
            const isReconnect = this.isReconnectable && !tunnel;
            await this.onConnectingToTunnel(tunnel);
            if (tunnel) {
                this.tunnel = tunnel;
                this.accessToken = TunnelAccessTokenProperties.getTunnelAccessToken(
                    tunnel,
                    this.tunnelAccessScope,
                );
            }
            if (!this.connector) {
                this.connector = this.createTunnelConnector();
            }
            await this.connector.connectSession(isReconnect, this.disposeToken);
        });
    }

    /**
     * Validate the tunnel and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     * @param tunnel Tunnel to use for the connection.
     *     Tunnel object to get the connection data if defined.
     *     Undefined if the connection data is already known.
     */
    public onConnectingToTunnel(tunnel?: Tunnel): Promise<void> {
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
}
