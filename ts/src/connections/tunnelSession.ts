// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Event } from 'vscode-jsonrpc';
import { Tunnel } from '@microsoft/dev-tunnels-contracts';
import { Stream, Trace, CancellationToken, SshDisconnectReason } from '@microsoft/dev-tunnels-ssh';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import * as http from 'http';
import { RefreshingTunnelEventArgs } from './refreshingTunnelEventArgs';
import { TunnelConnection } from './tunnelConnection';

/**
 * Tunnel session.
 */
export interface TunnelSession extends TunnelConnection {

    /**
     * Gets the tunnel.
     */
    tunnel: Tunnel | null;

    /**
     * Gets the trace source.
     */
    trace: Trace;

    /**
     * Gets tunnel access scope for this tunnel session.
     */
    tunnelAccessScope: string;

    /**
     * Gets the http agent for http requests.
     */
    httpAgent?: http.Agent;

    /**
     * Gets the disconnection reason.
     * {@link SshDisconnectReason.none} or undefined if not yet disconnected.
     * {@link SshDisconnectReason.connectionLost} if network connection was lost and reconnects are not enabled or unsuccesfull.
     * {@link SshDisconnectReason.byApplication} if connection was disposed.
     * {@link SshDisconnectReason.tooManyConnections} if host connection was disconnected because another host connected for the same tunnel.
     */
    readonly disconnectReason: SshDisconnectReason | undefined;

    /**
     * Validates tunnel access token if it's present. Returns the token.
     * Note: uses client's system time for the validation.
     */
    validateAccessToken(): string | undefined;

    /**
     *  Notifies about a connection retry, giving the client a chance to delay or cancel it.
     */
    onRetrying(event: RetryingTunnelConnectionEventArgs): void;

    /**
     * Validate {@link tunnel} and get data needed to connect to it, if the tunnel is provided;
     * otherwise, ensure that there is already sufficient data to connect to a tunnel.
     */
    onConnectingToTunnel(): Promise<void>;

    /**
     * @internal Connect to the tunnel session by running the provided {@link action}.
     */
    connectSession(action: () => Promise<void>): Promise<void>;

    /**
     * Connect to the tunnel session with the tunnel connector.
     * @param tunnel Tunnel to use for the connection.
     *     Undefined if the connection information is already known and the tunnel is not needed.
     *     Tunnel object to get the connection information from that tunnel.
     */
    connectTunnelSession(
        tunnel?: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<void>;

    /**
     * @internal Creates a stream to the tunnel for the tunnel session.
     */
    createSessionStream(
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<{ stream: Stream, protocol: string }>;

    /**
     * @internal Configures the tunnel session with the given stream.
     */
    configureSession(
        stream: Stream,
        protocol: string,
        isReconnect: boolean,
        cancellation?: CancellationToken,
    ): Promise<void>;

    /**
     * @internal Closes the tunnel session.
     */
    closeSession(reason?: SshDisconnectReason, error?: Error): Promise<void>;

    /**
     * @internal Refreshes the tunnel access token. This may be useful when the tunnel service responds with 401 Unauthorized.
     */
    refreshTunnelAccessToken(cancellation?: CancellationToken): Promise<boolean>;

    /**
     * An event which fires when tunnel connection refreshes tunnel.
     */
    readonly refreshingTunnel: Event<RefreshingTunnelEventArgs>;

    /**
     * @internal Start connecting relay client.
     */
    startConnecting(): void;

    /**
     * @internal Finish connecting relay client.
     */
    finishConnecting(reason?: SshDisconnectReason, disconnectError?: Error): void;
}
