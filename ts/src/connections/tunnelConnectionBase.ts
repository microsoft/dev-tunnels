// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { ObjectDisposedError } from '@microsoft/dev-tunnels-ssh';
import { CancellationToken, CancellationTokenSource, Emitter } from 'vscode-jsonrpc';
import { ConnectionStatus } from './connectionStatus';
import { ConnectionStatusChangedEventArgs } from './connectionStatusChangedEventArgs';
import { TunnelConnection } from './tunnelConnection';
import { RefreshingTunnelAccessTokenEventArgs } from './refreshingTunnelAccessTokenEventArgs';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { TunnelAccessTokenProperties } from '@microsoft/dev-tunnels-management';
import { ForwardedPortConnectingEventArgs } from '@microsoft/dev-tunnels-ssh-tcp';
import { TrackingEmitter } from './utils';
import { SshKeepAliveEventArgs } from './sshKeepAliveEventArgs';

/**
 * Tunnel connection base class.
 */
export class TunnelConnectionBase implements TunnelConnection {
    private readonly disposeCts = new CancellationTokenSource();
    private status = ConnectionStatus.None;
    private error?: Error;
    private readonly refreshingTunnelAccessTokenEmitter = 
        new TrackingEmitter<RefreshingTunnelAccessTokenEventArgs>();
    private readonly connectionStatusChangedEmitter =
        new Emitter<ConnectionStatusChangedEventArgs>();
    private readonly retryingTunnelConnectionEmitter =
        new Emitter<RetryingTunnelConnectionEventArgs>();
    private readonly forwardedPortConnectingEmitter =
        new Emitter<ForwardedPortConnectingEventArgs>();
    private readonly keepAliveFailedEmitter = new Emitter<SshKeepAliveEventArgs>();
    private readonly keepAliveSucceededEmitter = new Emitter<SshKeepAliveEventArgs>();

    protected constructor(
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

    protected get isRefreshingTunnelAccessTokenEventHandled() {
        return this.refreshingTunnelAccessTokenEmitter.isSubscribed;
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
            this.throwIfDisposed(`ConnectionStatus: ${value}`);
        }

        if (value === ConnectionStatus.RefreshingTunnelAccessToken && this.status !== ConnectionStatus.Connecting) {
            throw new Error('Refreshing tunnel access token is allowed only when connecting.');
        }

        if (value !== this.status) {
            const previousStatus = this.connectionStatus;
            this.status = value;
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
     * An event which fires when a connection is made to the forwarded port.
     */
    public readonly forwardedPortConnecting = this.forwardedPortConnectingEmitter.event;

    /**
     * Event raised when a keep-alive message response is not received.
     */
    public readonly keepAliveFailed = this.keepAliveFailedEmitter.event;

    /**
     * Event raised when a keep-alive message response is received.
     */
    public readonly keepAliveSucceeded = this.keepAliveSucceededEmitter.event;

    protected onForwardedPortConnecting(e: ForwardedPortConnectingEventArgs) {
        this.forwardedPortConnectingEmitter.fire(e);
    }

    /**
     * Raises the keep-alive failed event.
     */
    protected onKeepAliveFailed(count: number) {
        this.keepAliveFailedEmitter.fire(new SshKeepAliveEventArgs(count));
    }

    /**
     * Raises the keep-alive succeeded event.
     */
    protected onKeepAliveSucceeded(count: number) {
        this.keepAliveSucceededEmitter.fire(new SshKeepAliveEventArgs(count));
    }

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
        return event.tunnelAccessToken ? await event.tunnelAccessToken : undefined;
    }

    /**
     * Event fired when the connection status has changed.
     */
    protected onConnectionStatusChanged(
        previousStatus: ConnectionStatus,
        status: ConnectionStatus,
    ) {
        // Disconnect error is provided only during disconnection, not disposal.
        const disconnectError = this.connectionStatus === ConnectionStatus.Disconnected && !this.isDisposed ? this.disconnectError : undefined;
        
        const event = new ConnectionStatusChangedEventArgs(previousStatus, status, disconnectError);
        this.connectionStatusChangedEmitter.fire(event);
    }

    /**
     * Throws CancellationError if the tunnel connection is disposed.
     */
    protected throwIfDisposed(message: string, stack?: string) {
        if (this.isDisposed) {
            const error = new ObjectDisposedError(`The tunnel connection is disposed. ${message}`);
            if (stack) {
                error.stack = stack;
            }
            throw error;
        }
    }
}
