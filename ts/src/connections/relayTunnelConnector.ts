// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    CancellationError,
    CancellationToken,
    ObjectDisposedError,
    SshConnectionError,
    SshDisconnectReason,
    SshReconnectError,
    Stream,
    Trace,
    TraceLevel,
} from '@microsoft/dev-tunnels-ssh';
import { TunnelConnector } from './tunnelConnector';
import { delay, getErrorMessage } from './utils';
import { BrowserWebSocketRelayError, RelayConnectionError } from './sshHelpers';
import { RetryingTunnelConnectionEventArgs } from './retryingTunnelConnectionEventArgs';
import { TunnelSession } from './tunnelSession';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';


// After the 6th attemt, each next attempt will happen after a delay of 2^7 * 100ms = 12.8s
export const maxReconnectDelayMs = 13000;

// Delay between the 1st and the 2nd attempts.
const reconnectInitialDelayMs = 1000;

// There is no status code information in web socket errors in browser context.
// Instead, connection will retry anyway with limited retry attempts.
const maxBrowserReconnectAttempts = 5;

/**
 * Tunnel connector that connects a tunnel session to a web socket stream in the tunnel Relay service.
 */
export class RelayTunnelConnector implements TunnelConnector {
    public constructor(private readonly tunnelSession: TunnelSession) {}

    private get trace(): Trace {
        return this.tunnelSession.trace;
    }

    /**
     * Connect or reconnect tunnel SSH session.
     * @param isReconnect A value indicating if this is a reconnect (true) or regular connect (false).
     * @param cancellation Cancellation token.
     */
    public async connectSession(
        isReconnect: boolean,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<void> {
        let disconnectReason: SshDisconnectReason | undefined;
        let error: Error | undefined;

        function throwIfCancellation(e: any) {
            if ((e instanceof CancellationError && cancellation?.isCancellationRequested) ||
                e instanceof ObjectDisposedError) {
                error = undefined;
                disconnectReason = SshDisconnectReason.byApplication;
                throw e;
            }
        }

        function throwError(message: string) {
            if (error) {
                // Preserve the error object, just replace the message.
                error.message = message;
            } else {
                error = new Error(message);
            }

            throw error;
        }

        let browserReconnectAttempt = 0;
        let attemptDelayMs: number = reconnectInitialDelayMs;
        let isTunnelAccessTokenRefreshed = false;
        let isDelayNeeded = true;
        let errorDescription: string | undefined;
        this.tunnelSession.startConnecting();
        try {
            for (let attempt = 0; ; attempt++) {
                if (cancellation?.isCancellationRequested) {
                    throw new CancellationError();
                }

                if (attempt > 0) {
                    if (error) {
                        if (!(options?.enableRetry ?? true)) {
                            throw error;
                        }

                        const args = new RetryingTunnelConnectionEventArgs(error, attemptDelayMs);
                        this.tunnelSession.onRetrying(args);
                        if (!args.retry) {
                            // Stop retries.
                            throw error;
                        }

                        if (args.delayMs >= reconnectInitialDelayMs) {
                            attemptDelayMs = args.delayMs;
                        } else {
                            isDelayNeeded = false;
                        }
                    }

                    const retryTiming = isDelayNeeded
                        ? ` in ${
                            attemptDelayMs < 1000
                                ? `0.${attemptDelayMs / 100}s`
                                : `${attemptDelayMs / 1000}s`
                        }`
                        : '';
                    this.trace(
                        TraceLevel.Verbose,
                        0,
                        `Error connecting to tunnel SSH session, retrying${retryTiming}${
                            errorDescription ? `: ${errorDescription}` : ''
                        }`,
                    );

                    if (isDelayNeeded) {
                        try {
                            await delay(attemptDelayMs, cancellation);
                        } catch (e) {
                            throwIfCancellation(e);
                            throw e;
                        }

                        if (attemptDelayMs < maxReconnectDelayMs) {
                            attemptDelayMs = attemptDelayMs << 1;
                        }
                    }
                }

                isDelayNeeded = true;
                let stream: Stream | undefined = undefined;
                errorDescription = undefined;
                disconnectReason = SshDisconnectReason.connectionLost;
                error = undefined;
                try {
                    const streamAndProtocol = await this.tunnelSession.createSessionStream(
                        options, cancellation);
                    stream = streamAndProtocol.stream;

                    await this.tunnelSession.configureSession(
                        stream, streamAndProtocol.protocol, isReconnect, cancellation);

                    stream = undefined;
                    disconnectReason = undefined;
                    return;
                } catch (e) {
                    if (!(e instanceof Error)) {
                        // Not recoverable if we cannot recognize the error object.
                        throwError(
                            `Failed to connect to the tunnel service and start tunnel SSH session: ${e}`,
                        );
                    }

                    throwIfCancellation(e);
                    error = <Error>e;
                    errorDescription = error.message;

                    // Browser web socket relay error - retry until max number of attempts is exceeded.
                    if (e instanceof BrowserWebSocketRelayError) {
                        if (browserReconnectAttempt++ >= maxBrowserReconnectAttempts) {
                            throw e;
                        }
                        continue;
                    }

                    // SSH reconnection error. Disable reconnection and try again without delay.
                    if (e instanceof SshReconnectError) {
                        disconnectReason = SshDisconnectReason.protocolError;
                        isDelayNeeded = false;
                        isReconnect = false;
                        continue;
                    }

                    // SSH connection error. Only 'connection lost' is recoverable.
                    if (e instanceof SshConnectionError) {
                        const reason = (e as SshConnectionError).reason;
                        if (reason === SshDisconnectReason.connectionLost) {
                            continue;
                        }
                        disconnectReason = reason || SshDisconnectReason.byApplication;
                        throwError(`Failed to start tunnel SSH session: ${errorDescription}`);
                    }

                    // Web socket connection error
                    if (e instanceof RelayConnectionError) {
                        const statusCode = (e as RelayConnectionError).errorContext?.statusCode;
                        const statusCodeText = statusCode ? ` (${statusCode})` : '';
                        switch (errorDescription) {
                            case 'error.relayClientUnauthorized': {
                                const notAuthorizedText = 'Not authorized' + statusCodeText;
                                disconnectReason = SshDisconnectReason.authCancelledByUser;
                                if (isTunnelAccessTokenRefreshed) {
                                    // We've already refreshed the tunnel access token once.
                                    throwError(
                                        `${notAuthorizedText}. Refreshed tunnel access token also does not work.`,
                                    );
                                }
                                try {
                                    isTunnelAccessTokenRefreshed = await this.tunnelSession.refreshTunnelAccessToken(
                                        cancellation,
                                    );
                                } catch (refreshError) {
                                    throwIfCancellation(refreshError);
                                    throwError(
                                        `${notAuthorizedText}. Refreshing tunnel access token failed with error ${getErrorMessage(
                                            refreshError,
                                        )}`,
                                    );
                                }

                                if (!isTunnelAccessTokenRefreshed) {
                                    throwError(
                                        `${notAuthorizedText}. Provide a fresh tunnel access token with '${this.tunnelSession.tunnelAccessScope}' scope.`,
                                    );
                                }

                                isDelayNeeded = false;
                                errorDescription =
                                    'The tunnel access token was no longer valid and had just been refreshed.';
                                continue;
                            }
                            case 'error.relayClientForbidden':
                                disconnectReason = SshDisconnectReason.authCancelledByUser;
                                throwError(
                                    `Forbidden${statusCodeText}. Provide a fresh tunnel access token with '${this.tunnelSession.tunnelAccessScope}' scope.`,
                                );
                                break;

                            case 'error.tunnelPortNotFound':
                                throwError(`The tunnel or port is not found${statusCodeText}`);
                                break;

                            // Normally nginx choses another healthy pod when it cannot establish connection to a pod.
                            // However, if there are no other pods, it may returns 502 (Bad Gateway) to the client.
                            // This rare case may happen when the cluster recovers from a failure
                            // and the nginx controller has started but Relay service has not yet.
                            
                            // 503 (Service Unavailable) can happen when Relay calls control plane to authenticate the request,
                            // control plane hits 429s from Cosmos DB and replies back with 503.

                            // 429 (Too Many Requests) can happen if client exceeds request rate limits.
                            case 'error.badGateway':
                            case 'error.serviceUnavailable':
                            case 'error.tooManyRequests':
                                errorDescription = errorDescription === 'error.tooManyRequests' 
                                    ? `Rate limit exceeded${statusCodeText}. Too many requests in a given amount of time.`
                                    : `Service temporarily unavailable${statusCodeText}`;
                                disconnectReason = SshDisconnectReason.serviceNotAvailable;

                                // Do not attempt more than 3 times to not overwhelm the service.
                                if (attempt > 3) {
                                    throwError(errorDescription);
                                }

                                // Increase the attempt delay to reduce load on the service and let it recover.
                                if (attemptDelayMs < maxReconnectDelayMs / 2) {
                                    attemptDelayMs = maxReconnectDelayMs / 2;
                                }

                                continue;

                            default:
                                if (errorDescription?.startsWith('error.relayConnectionError ')) {
                                    const recoverableError = recoverableNetworkErrors.find((s) =>
                                        errorDescription!.includes(s),
                                    );
                                    if (recoverableError) {
                                        errorDescription = `Failed to connect to Relay server: ${recoverableError}`;
                                        continue;
                                    }
                                }
                        }
                    }

                    // Everything else is not recoverable
                    throw e;
                } finally {
                    // Graft SSH disconnect reason on to the error object as 'reason' property.
                    if (error && disconnectReason && !(<any>error).reason) {
                        (<any>error).reason = disconnectReason;
                    }

                    if (disconnectReason) {
                        await this.tunnelSession.closeSession(disconnectReason, error);
                    }

                    if (stream) {
                        await stream.close(error);
                    }
                }
            }
        } finally {
            this.tunnelSession.finishConnecting(disconnectReason, error);
        }
    }
}

const recoverableNetworkErrors = [
    'ECONNRESET',
    'ENOTFOUND',
    'ESOCKETTIMEDOUT',
    'ETIMEDOUT',
    'ECONNREFUSED',
    'EHOSTUNREACH',
    'EPIPE',
    'EAI_AGAIN',
    'EBUSY',
];
