// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationError } from '@microsoft/dev-tunnels-ssh';
import { CancellationToken, CancellationTokenSource, Emitter } from 'vscode-jsonrpc';
import { ConnectionStatus } from './connectionStatus';
import { ConnectionStatusChangedEventArgs } from './connectionStatusChangedEventArgs';
import { TunnelConnection } from './tunnelConnection';
import { RefreshingTunnelAccessTokenEventArgs } from './refreshingTunnelAccessTokenEventArgs';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { TunnelAccessTokenProperties } from '@vs/tunnels-management';

/**
 * Tunnel connection base class.
 */
export class TunnelConnectionBase implements TunnelConnection {
    private readonly disposeCts = new CancellationTokenSource();
    private status = ConnectionStatus.None;
    private error?: Error;
    protected isRefreshingTunnelAccessTokenEventHandled = false;
    private readonly refreshingTunnelAccessTokenEmitter = new Emitter<
        RefreshingTunnelAccessTokenEventArgs
    >({
        onFirstListenerAdd: () => (this.isRefreshingTunnelAccessTokenEventHandled = true),
        onLastListenerRemove: () => (this.isRefreshingTunnelAccessTokenEventHandled = false),
    });
    private readonly connectionStatusChangedEmitter = new Emitter<
        ConnectionStatusChangedEventArgs
    >();
    private readonly retryingTunnelConnectionEmitter = new Emitter<
        RetryingTunnelConnectionEventArgs
    >();

    constructor(
        /**
         * Gets tunnel access scope for this tunnel session.
         */
        public readonly tunnelAccessScope: string,
    ) {}

    /**
     * Gets a value indicathing that this tunnel connection session is disposed.
     */
    public get isDisposed() {
        return this.disposeCts.token.isCancellationRequested;
    }

    /**
     * Gets dispose cancellation token.
     */
    protected get disposeToken(): CancellationToken {
        return this.disposeCts.token;
    }

    /**
     * Gets the connection status.
     */
    public get connectionStatus(): ConnectionStatus {
        return this.status;
    }

    /**
     * Sets the connection status.
     * Throws CancellationError if the session is disposed and the status being set is not ConnectionStatus.Disconnected.
     */
    protected set connectionStatus(value: ConnectionStatus) {
        if (this.isDisposed && value !== ConnectionStatus.Disconnected) {
            this.throwIfDisposed();
        }

        if (value !== this.status) {
            const previousStatus = this.connectionStatus;
            this.status = value;
            if (value === ConnectionStatus.Connected) {
                this.error = undefined;
            }
            this.onConnectionStatusChanged(previousStatus, value);
        }
    }

    /**
     * Gets the error that caused disconnection.
     * Undefined if not yet connected or disconnection was caused by disposing of this object.
     */
    public get disconnectError(): Error | undefined {
        return this.error;
    }

    /**
     * Sets the error that caused disconnection.
     */
    protected set disconnectError(e: Error | undefined) {
        this.error = e;
    }

    /**
     * Event for refreshing the tunnel access token.
     * The tunnel client will fire this event when it is not able to use the access token it got from the tunnel.
     */
    public readonly refreshingTunnelAccessToken = this.refreshingTunnelAccessTokenEmitter.event;

    /**
     * Connection status changed event.
     */
    public readonly connectionStatusChanged = this.connectionStatusChangedEmitter.event;

    /**
     * Event raised when a tunnel connection attempt failed and is about to be retried.
     *  An event handler can cancel the retry by setting {@link RetryingTunnelConnectionEventArgs.retry} to false.
     */
    public readonly retryingTunnelConnection = this.retryingTunnelConnectionEmitter.event;

    /**
     * Closes and disposes the tunnel session.
     */
    public dispose(): Promise<void> {
        this.disposeCts.cancel();
        this.connectionStatus = ConnectionStatus.Disconnected;
        return Promise.resolve();
    }

    /**
     *  Notifies about a connection retry, giving the relay client a chance to delay or cancel it.
     */
    public onRetrying(event: RetryingTunnelConnectionEventArgs): void {
        this.retryingTunnelConnectionEmitter.fire(event);
    }

    /**
     * Gets the fresh tunnel access token or undefined if it cannot.
     */
    protected async getFreshTunnelAccessToken(
        cancellation: CancellationToken,
    ): Promise<string | null | undefined> {
        const event = new RefreshingTunnelAccessTokenEventArgs(
            this.tunnelAccessScope,
            cancellation,
        );
        this.refreshingTunnelAccessTokenEmitter.fire(event);
        const result = event.tunnelAccessToken ? await event.tunnelAccessToken : undefined;
        if (result) {
            TunnelAccessTokenProperties.validateTokenExpiration(result);
        }
        return result;
    }

    /**
     * Event fired when the connection status has changed.
     */
    protected onConnectionStatusChanged(
        previousStatus: ConnectionStatus,
        status: ConnectionStatus,
    ) {
        const event = new ConnectionStatusChangedEventArgs(
            previousStatus,
            status,
            this.disconnectError,
        );
        this.connectionStatusChangedEmitter.fire(event);
    }

    /**
     * Throws CancellationError if the tunnel connection is disposed.
     */
    protected throwIfDisposed() {
        if (this.isDisposed) {
            throw new CancellationError('The tunnel connection is disposed.');
        }
    }
}
