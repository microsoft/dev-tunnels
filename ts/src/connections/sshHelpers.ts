// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as ssh from '@microsoft/dev-tunnels-ssh';
import { IncomingMessage } from 'http';
import {
    client as WebSocketClient,
    connection as WebSocketConnection,
    IClientConfig,
} from 'websocket';
 

declare module 'websocket' {
    interface client {
        /**
         * 'httpResponse' event in WebSocketClient is fired when the server responds but the HTTP request doesn't properly upgrade to a web socket,
         * i.e. the status code is not 101 `Switching Protocols`. The argument of the event callback is the recieved response.
         */
        on(event: 'httpResponse', cb: (response: IncomingMessage) => void): this;
    }
}

/**
 * Error class for errors connecting to a web socket in non-node (browser) context.
 * There is no status code or underlying network error info in the browser context.
 */
export class BrowserWebSocketRelayError extends Error {
    public constructor(message?: string) {
        super(message);
    }
}
/**
 * Ssh connection helper
 */
export class SshHelpers {
    /**
     * Open a connection to the relay uri depending on the running environment.
     * @param relayUri
     * @param protocols
     * @param headers
     * @param clientConfig
     * @returns
     */
    public static openConnection(
        relayUri: string,
        protocols?: string[],
        headers?: object,
        clientConfig?: IClientConfig,
    ): Promise<ssh.WebSocketStream> {
        if (isNode()) {
            return SshHelpers.nodeSshStreamFactory(relayUri, protocols, headers, clientConfig);
        }

        return SshHelpers.webSshStreamFactory(new WebSocket(relayUri, protocols));
    }

    /**
     * Creates a client SSH session with standard configuration for tunnels.
     * @param configure Optional callback for additional session configuration.
     * @returns The created SSH session.
     */
    public static createSshClientSession(
        configure?: (config: ssh.SshSessionConfiguration) => void,
    ): ssh.SshClientSession {
        return SshHelpers.createSshSession((config) => {
            if (configure) configure(config);
            return new ssh.SshClientSession(config);
        });
    }

    /**
     * Creates a SSH server session with standard configuration for tunnels.
     * @param reconnectableSessions Optional list that tracks reconnectable sessions.
     * @param configure Optional callback for additional session configuration.
     * @returns The created SSH session.
     */
    public static createSshServerSession(
        reconnectableSessions?: ssh.SshServerSession[],
        configure?: (config: ssh.SshSessionConfiguration) => void,
    ): ssh.SshServerSession {
        return SshHelpers.createSshSession((config) => {
            if (configure) configure(config);
            return new ssh.SshServerSession(config, reconnectableSessions);
        });
    }

    /**
     * Create a websocketStream from a connection.
     * @param connection
     * @returns
     */
    public static createWebSocketStreamAdapter(connection: WebSocketConnection) {
        return new ssh.WebSocketStream(new WebsocketStreamAdapter(connection));
    }

    /**
     * Set up a web Ssh stream factory.
     * @param socket
     * @returns
     */
    public static webSshStreamFactory(socket: WebSocket): Promise<ssh.WebSocketStream> {
        socket.binaryType = 'arraybuffer';
        return new Promise<ssh.WebSocketStream>((resolve, reject) => {
            socket.onopen = () => {
                resolve(new ssh.WebSocketStream(socket));
            };
            socket.onerror = (e) => {
                // Note: as per web socket guidance https://websockets.spec.whatwg.org/#eventdef-websocket-error,
                // the user agents must not convey extended error information including the cases where the server
                // didn't complete the opening handshake (e.g. because it was not a WebSocket server).
                // So we cannot obtain the response status code.
                reject(new BrowserWebSocketRelayError(`Failed to connect to relay url`));
            };
        });
    }

    private static createSshSession<T>(
        factoryCallback: (config: ssh.SshSessionConfiguration) => T,
    ): T {
        const config = new ssh.SshSessionConfiguration();
        config.keyExchangeAlgorithms.splice(0);
        config.keyExchangeAlgorithms.push(ssh.SshAlgorithms.keyExchange.ecdhNistp384Sha384);
        config.keyExchangeAlgorithms.push(ssh.SshAlgorithms.keyExchange.ecdhNistp256Sha256);
        config.keyExchangeAlgorithms.push(ssh.SshAlgorithms.keyExchange.dhGroup14Sha256);

        return factoryCallback(config);
    }

    private static nodeSshStreamFactory(
        relayUri: string,
        protocols?: string[],
        headers?: object,
        clientConfig?: IClientConfig,
    ): Promise<ssh.WebSocketStream> {
        const client = new WebSocketClient(clientConfig);
        return new Promise<ssh.WebSocketStream>((resolve, reject) => {
            client.on('connect', (connection: any) => {
                resolve(new ssh.WebSocketStream(new WebsocketStreamAdapter(connection)));
            });

            // If the server responds but doesn't properly upgrade the connection to web socket, WebSocketClient fires 'httpResponse' event.
            // TODO: Return ProblemDetails from TunnelRelay service
            client.on('httpResponse', ({ statusCode, statusMessage }) => {
                const errorContext = webSocketClientContexts.find(
                    (c) => c.statusCode === statusCode,
                ) ?? {
                    statusCode,
                    errorType: RelayErrorType.ServerError,
                    error: `relayConnectionError Server responded with a non-101 status: ${statusCode} ${statusMessage}`,
                };

                reject(new RelayConnectionError(`error.${errorContext.error}`, errorContext));
            });

            // All other failure cases - cannot connect and get the response, or the web socket handshake failed.
            client.on('connectFailed', ({ message }) => {
                if (message && message.startsWith('Error: ')) {
                    message = message.substr(7);
                }

                const errorContext = webSocketClientContexts.find(
                    (c) => c.regex && c.regex.test(message),
                ) ?? {
                    // Other errors are most likely connectivity issues.
                    // The original error message may have additional helpful details.
                    errorType: RelayErrorType.ServerError,
                    error: `relayConnectionError ${message}`,
                };

                reject(new RelayConnectionError(`error.${errorContext.error}`, errorContext));
            });

            client.connect(relayUri, protocols, undefined, headers);
        });
    }
}

/**
 * Partially adapts a Node websocket connection object to the browser websocket API,
 * enough so that it can be used as an SSH stream.
 */
class WebsocketStreamAdapter {
    public constructor(private connection: WebSocketConnection) {}

    public get protocol(): string | undefined {
        return this.connection.protocol;
    }

    public set onmessage(messageHandler: ((e: { data: ArrayBuffer }) => void) | null) {
        if (messageHandler) {
            this.connection.on('message', (message: any) => {
                // This assumes all messages are binary.
                messageHandler({ data: message.binaryData! });
            });
        } else {
            // Removing event handlers is not implemented.
        }
    }

    public set onclose(
        closeHandler: ((e: { code?: number; reason?: string; wasClean: boolean }) => void) | null,
    ) {
        if (closeHandler) {
            this.connection.on('close', (code: any, reason: any) => {
                closeHandler({ code, reason, wasClean: !(code || reason) });
            });
        } else {
            // Removing event handlers is not implemented.
        }
    }

    public send(data: ArrayBuffer): void {
        if (Buffer.isBuffer(data)) {
            this.connection.sendBytes(data);
        } else {
            this.connection.sendBytes(Buffer.from(data));
        }
    }

    public close(code?: number, reason?: string): void {
        if (code || reason) {
            this.connection.drop(code, reason);
        } else {
            this.connection.close();
        }
    }
}

/**
 * Helper function to check the running environment.
 */
export const isNode = (): boolean =>
    typeof process !== 'undefined' &&
    typeof process.release !== 'undefined' &&
    process.release.name === 'node';

/**
 * A workspace connection info
 */
export interface IWorkspaceConnectionInfo {
    id: string;
    relayLink?: string;
    relaySas?: string;
    hostPublicKeys: string[];
    isHostConnected?: boolean;
}

/**
 * The ssh session authenticate options
 */
export interface ISshSessionAuthenticateOptions {
    sessionToken: string;
    relaySas: string;
}

/**
 * The workspace session info required to join
 */
export type IWorkspaceSessionInfo = IWorkspaceConnectionInfo & ISshSessionAuthenticateOptions;

/**
 * A shared workspace info
 */
export interface ISharedWorkspaceInfo extends IWorkspaceConnectionInfo {
    name: string;
    joinLink: string;
    conversationId: string;
}

/**
 * Type of relay connection error types.
 */
export enum RelayErrorType {
    ConnectionError = 1,
    Unauthorized = 2,

    /**
     * @deprecated This relay error type is not used.
     */
    EndpointNotFound = 3,
    /**
     * @deprecated This relay error type is not used.
     */
    ListenerOffline = 4,

    ServerError = 5,
    TunnelPortNotFound = 6,
    TooManyRequests = 7,
    ServiceUnavailable = 8,
}
/**
 * Error used when a connection to the tunnel relay failed.
 */
export class RelayConnectionError extends Error {
    public constructor(
        message: string,
        public readonly errorContext: {
            errorType: RelayErrorType;
            statusCode?: number;
        },
    ) {
        super(message);
    }
}

/**
 * Web socket client error context.
 */
interface WebSocketClientErrorContext {
    readonly regex?: RegExp;
    readonly statusCode?: number;
    readonly error: string;
    readonly errorType: RelayErrorType;
}

/**
 * Web socket client error contexts.
 */

// TODO: Return ProblemDetails from TunnelRelay service.
const webSocketClientContexts: WebSocketClientErrorContext[] = [
    {
        regex: /status: 401/,
        statusCode: 401,
        error: 'relayClientUnauthorized',
        errorType: RelayErrorType.Unauthorized,
    },
    {
        regex: /status: 403/,
        statusCode: 403,
        error: 'relayClientForbidden',
        errorType: RelayErrorType.Unauthorized,
    },
    {
        regex: /status: 404/,
        statusCode: 404,
        error: 'tunnelPortNotFound',
        errorType: RelayErrorType.TunnelPortNotFound,
    },
    {
        regex: /status: 429/,
        statusCode: 429,
        error: 'tooManyRequests',
        errorType: RelayErrorType.TooManyRequests,
    },
    {
        regex: /status: 500/,
        statusCode: 500,
        error: 'relayServerError',
        errorType: RelayErrorType.ServerError,
    },
    {
        regex: /status: 503/,
        statusCode: 503,
        error: 'serviceUnavailable',
        errorType: RelayErrorType.ServiceUnavailable,
    },
];
