// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as http from 'http';

/**
 * Options for a tunnel host or client connection.
 */
export interface TunnelConnectionOptions {
    /**
     * Gets or sets a value indicating whether the connection will be automatically retried after
     * a connection failure.
     *
     * The default value is true. When enabled, retries continue until the connection is
     * successful, the cancellation token is cancelled, or an unrecoverable error is encountered.
     *
     * Recoverable errors include network connectivity issues, authentication issues (e.g. expired
     * access token which may be refreshed before retrying), and service temporarily unavailable
     * (HTTP 503). For rate-limiting errors (HTTP 429) only a limited number of retries are
     * attempted before giving up.
     *
     * Retries are performed with exponential backoff, starting with a 100ms delay and doubling
     * up to a maximum 12s delay, with further retries using the same max delay.
     *
     * Note after the initial connection succeeds, the host or client may still become disconnected
     * at any time after that. In that case the `enableReconnect` option controls whether an
     * automatic reconnect will be attempted. Reconnection has the same retry behavior.
     *
     * Listen to the `retryingTunnelConnection` event to be notified when the connection is
     * retrying.
     */
    enableRetry?: boolean;

    /**
     * Gets or sets a value indicating whether the connection will attempt to automatically
     * reconnect (with no data loss) after a disconnection.
     * 
     * The default value is true.
     *
     * If reconnection fails, or is not enabled, the application may still attempt to connect
     * the client again, however in that case no state is preserved.
     *
     * Listen to the `connectionStatusChanged` event to be notified when reconnection or
     * disconnection occurs.
     */
    enableReconnect?: boolean;

    /**
     * Gets or sets the HTTP agent to use for the connection.
     */
    httpAgent?: http.Agent;

    /**
     * Gets or sets the ID of the tunnel host to connect to, if there are multiple
     * hosts accepting connections on the tunnel, or null to connect to a single host
     * (most common). This option applies only to client connections.
     */
    hostId?: string;

    /**
     * Gets or sets the SSH keep-alive interval in seconds. Default is 0 (disabled).
     * When set to a positive value, the client/host will send SSH keep-alive messages
     * and raise keep-alive events with the number of consecutive successes or failures.
     * 
     * The keep-alive events are raised at the time of sending the next keep-alive request.
     * For example, if the interval is set to 10 seconds, the first request is sent after
     * 10 seconds of inactivity and waits for 10 more seconds to call the keep-alive
     * callback before sending another keep-alive request.
     */
    keepAliveIntervalInSeconds?: number;
}
