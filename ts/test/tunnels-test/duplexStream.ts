// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


import * as http from 'http';
import {
    server as WebSocketServer,
    request as WebSocketRequest,
    connection as WebSocketConnection,
    w3cwebsocket as W3CWebSocket,
} from 'websocket';
import { BaseStream, Stream, WebSocketStream } from '@microsoft/dev-tunnels-ssh';
import { AddressInfo } from 'net';

/**
 * Creates a pair of connected stream adapters for testing purposes.
 */
export class DuplexStream extends BaseStream {
    private other!: DuplexStream;

    public static async createStreams(type?: string): Promise<[Stream, Stream]> {
        if (type === 'ws') {
            return await createWebSocketStreams();
        }

        const stream1 = new DuplexStream();
        const stream2 = new DuplexStream();
        stream1.other = stream2;
        stream2.other = stream1;
        return [stream1, stream2];
    }

    private constructor() {
        super();
    }

    public async write(data: Buffer): Promise<void> {
        if (!data) throw new TypeError('Data is required.');
        this.other.onData(Buffer.from(data));
    }

    public async close(error?: Error): Promise<void> {
        if (!error) {
            this.dispose();
            this.other.onEnd();
            this.other.dispose();
        } else {
            this.onError(error);
            this.dispose();
            this.other.onError(error);
            this.other.dispose();
        }
    }

    public dispose(): void {
        super.dispose();

        if (!this.other.isDisposed) {
            this.other.onError(new Error('Stream disposed.'));
            this.other.dispose();
        }
    }
}

let wsServer: WebSocketServer;
let wsPort: number;

async function createWebSocketStreams(): Promise<[Stream, Stream]> {
    await createWebSocketServer();

    const serverConnectionPromise = new Promise<WebSocketConnection>((resolve) => {
        wsServer.once('request', (request: WebSocketRequest) => {
            const connection = request.accept();
            resolve(connection);
        });
    });

    const clientSocket = new W3CWebSocket('ws://localhost:' + wsPort);
    const clientConnectionPromise = new Promise<void>((resolve, reject) => {
        clientSocket.onopen = () => {
            resolve();
        };
        clientSocket.onerror = (e) => {
            reject(new Error('Connection failed.'));
        };
    });

    await Promise.all([serverConnectionPromise, clientConnectionPromise]);

    const serverConnection = await serverConnectionPromise;
    return [new WebSocketServerStream(serverConnection), new WebSocketStream(clientSocket)];
}

async function createWebSocketServer() {
    if (wsServer) {
        return;
    }

    const httpServer = await new Promise<http.Server>((resolve) => {
        const s = http.createServer((request, response) => {
            response.writeHead(404);
            response.end();
        });
        s.listen(0, () => {
            resolve(s);
        });
    });

    wsPort = (<AddressInfo>httpServer.address()).port;
    wsServer = new WebSocketServer({
        httpServer,
        autoAcceptConnections: false,
    });
}

export function shutdownWebSocketServer() {
    if (wsServer) {
        wsServer.shutDown();
        (wsServer.config!.httpServer as any)[0].close();
        wsServer = <any>undefined;
    }
}

class WebSocketServerStream extends BaseStream {
    public constructor(private readonly connection: WebSocketConnection) {
        super();
        if (!connection) throw new TypeError('Connection is required.');

        connection.on('message', (data: any) => {
            if (data.type === 'binary') {
                this.onData(Buffer.from(data.binaryData));
            }
        });
        connection.on('close', (code, reason) => {
            if (!code && !reason) {
                this.onEnd();
            } else {
                const error = new Error(reason);
                (<any>error).code = code;
                this.onError(new Error(reason));
            }
        });
    }

    public async write(data: Buffer): Promise<void> {
        if (!data) throw new TypeError('Data is required.');
        this.connection.send(data);
    }

    public async close(error?: Error): Promise<void> {
        if (!error) {
            this.connection.close();
        } else {
            const code = typeof (<any>error).code === 'number' ? (<any>error).code : undefined;
            this.connection.drop(code, error.message);
        }
        this.onError(error || new Error('Stream closed.'));
    }
}
