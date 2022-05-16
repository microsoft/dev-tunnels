// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as ssh from '@vs/vs-ssh';
import {
    client as WebSocketClient,
    connection as WebSocketConnection,
    IClientConfig,
} from 'websocket';

/**
 * Ssh connection helper
 */
export class SshHelpers {
    /**
     * Get the Azure relay url from the relayUrl and relaySas key.
     * @param relayUrl
     * @param relaySas
     * @param action
     * @returns
     */
    public static getRelayUri(
        relayUrl: string,
        relaySas: string | undefined,
        action: string,
    ): string {
        if (!relayUrl) {
            throw new Error('Does not have a relay endpoint.');
        }

        // Reference:
        // https://github.com/Azure/azure-relay-node/blob/7b57225365df3010163bf4b9e640868a02737eb6/hyco-ws/index.js#L107-L137
        const relayUri =
            relayUrl.replace('sb:', 'wss:').replace('.net/', '.net:443/$hc/') +
            `?sb-hc-action=${action}&sb-hc-token=` +
            encodeURIComponent(relaySas || '');
        return relayUri;
    }

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
    ): Promise<ssh.Stream> {
        if (isNode()) {
            return SshHelpers.nodeSshStreamFactory(relayUri, protocols, headers, clientConfig);
        }

        return SshHelpers.webSshStreamFactory(new WebSocket(relayUri, protocols));
    }

    /**
     *
     * @returns Create a Ssh client session.
     */
    public static createSshClientSession(): ssh.SshClientSession {
        return SshHelpers.createSshSession((config) => new ssh.SshClientSession(config));
    }

    /**
     * Create a Ssh server session.
     * @param reconnectableSessions
     * @returns
     */
    public static createSshServerSession(
        reconnectableSessions?: ssh.SshServerSession[],
    ): ssh.SshServerSession {
        return SshHelpers.createSshSession(
            (config) => new ssh.SshServerSession(config, reconnectableSessions),
        );
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
    public static webSshStreamFactory(socket: WebSocket): Promise<ssh.Stream> {
        socket.binaryType = 'arraybuffer';
        return new Promise<ssh.Stream>((resolve, reject) => {
            socket.onopen = () => {
                resolve(new ssh.WebSocketStream(socket));
            };
            socket.onerror = (e) => {
                reject(new Error(`Failed to connect to relay url`));
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
        config.protocolExtensions.push(ssh.SshProtocolExtensionNames.sessionReconnect);
        config.protocolExtensions.push(ssh.SshProtocolExtensionNames.sessionLatency);

        // TODO: remove this once we know the ssh server has the > 3.3.10 update
        const posGcm = config.encryptionAlgorithms.indexOf(ssh.SshAlgorithms.encryption.aes256Gcm);
        if (posGcm !== -1) {
            config.encryptionAlgorithms.splice(posGcm, 1);
        }

        return factoryCallback(config);
    }

    private static nodeSshStreamFactory(
        relayUri: string,
        protocols?: string[],
        headers?: object,
        clientConfig?: IClientConfig,
    ): Promise<ssh.Stream> {
        const client = new WebSocketClient(clientConfig);
        return new Promise<ssh.Stream>((resolve, reject) => {
            client.on('connect', (connection: any) => {
                resolve(new ssh.WebSocketStream(new WebsocketStreamAdapter(connection)));
            });
            client.on('connectFailed', (e: any) => {
                if (e.message && e.message.startsWith('Error: ')) {
                    e.message = e.message.substr(7);
                }

                let errorType = RelayErrorType.ServerError;

                // Unfortunately the status code can only be obtained from the error message.
                // Also status 404 may be used for at least two distinct error conditions.
                // So we have to match on the error message text. This could break when
                // the relay server behavior changes or when updating the client websocket library.
                // But then in the worst case the original error message will be reported.

                // TODO: Return ProblemDetails from TunnelRelay service. The 404 error messages
                // below match Azure Relay but not TunnelRelay.
                if (/status: 401/.test(e.message)) {
                    e.message = 'error.relayClientUnauthorized';
                    errorType = RelayErrorType.Unauthorized;
                } else if (/status: 403/.test(e.message)) {
                    e.message = 'error.relayClientForbidden';
                    errorType = RelayErrorType.Unauthorized;
                } else if (/status: 404 Endpoint does not exist/.test(e.message)) {
                    e.message = 'error.relayEndpointNotFound';
                    errorType = RelayErrorType.EndpointNotFound;
                } else if (/status: 404 There are no listeners connected/.test(e.message)) {
                    e.message = 'error.relayListenerOffline';
                    errorType = RelayErrorType.ListenerOffline;
                } else if (/status: 500/.test(e.message)) {
                    e.message = 'error.relayServerError';
                    errorType = RelayErrorType.ServerError;
                } else {
                    // Other errors are most likely connectivity issues.
                    // The original error message may have additional helpful details.
                    e.message = 'error.relayConnectionError' + ' ' + e.message;
                }

                reject(new RelayConnectionError(e.message, { errorType }));
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
    constructor(private connection: WebSocketConnection) {}

    set onmessage(messageHandler: ((e: { data: ArrayBuffer }) => void) | null) {
        if (messageHandler) {
            this.connection.on('message', (message: any) => {
                // This assumes all messages are binary.
                messageHandler({ data: message.binaryData! });
            });
        } else {
            // Removing event handlers is not implemented.
        }
    }

    set onclose(
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
    EndpointNotFound = 3,
    ListenerOffline = 4,
    ServerError = 5,
}
/**
 * Error used when a connection to an Azure relay failed.
 */
export class RelayConnectionError extends Error {
    public constructor(
        message: string,
        public readonly errorContext: {
            errorType: RelayErrorType;
        },
    ) {
        super(message);
    }
}
