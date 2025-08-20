// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Event } from 'vscode-jsonrpc';
import { ConnectionStatus } from './connectionStatus';
import { ConnectionStatusChangedEventArgs } from './connectionStatusChangedEventArgs';
import { RefreshingTunnelAccessTokenEventArgs } from './refreshingTunnelAccessTokenEventArgs';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { ForwardedPortConnectingEventArgs } from '@microsoft/dev-tunnels-ssh-tcp';
import { SshKeepAliveEventArgs } from './sshKeepAliveEventArgs';

/**
 * Tunnel connection.
 */
export interface TunnelConnection {
    /**
     * Gets the connection status.
     */
    readonly connectionStatus: ConnectionStatus;

    /**
     * Gets the error that caused disconnection.
     * Undefined if not yet connected or disconnection was caused by disposing of this object.
     */
    readonly disconnectError?: Error;

    /**
     * Event for refreshing the tunnel access token.
     * The tunnel client will fire this event when it is not able to use the access token it got from the tunnel.
     */
    readonly refreshingTunnelAccessToken: Event<RefreshingTunnelAccessTokenEventArgs>;

    /**
     * Connection status changed event.
     */
    readonly connectionStatusChanged: Event<ConnectionStatusChangedEventArgs>;

    /**
     * Event raised when a tunnel connection attempt failed and is about to be retried.
     *  An event handler can cancel the retry by setting {@link RetryingTunnelConnectionEventArgs.retry} to false.
     */
    readonly retryingTunnelConnection: Event<RetryingTunnelConnectionEventArgs>;

    /**
     * An event which fires when a connection is made to the forwarded port.
     */
    readonly forwardedPortConnecting: Event<ForwardedPortConnectingEventArgs>;

    /**
     * Event raised when a keep-alive message response is not received.
     */
    readonly keepAliveFailed: Event<SshKeepAliveEventArgs>;

    /**
     * Event raised when a keep-alive message response is received.
     */
    readonly keepAliveSucceeded: Event<SshKeepAliveEventArgs>;

     /**
      * Disposes this tunnel session.
      */
    dispose(): Promise<void>;
}
