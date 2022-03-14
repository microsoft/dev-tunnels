import * as rpc from 'vscode-jsonrpc';
import * as ssh from '@vs/vs-ssh';

import { connection as WebSocketConnection } from 'websocket';
import { isNode, SshHelpers } from './sshHelpers';
import { HybridConnectionsWebSocketServer } from './hybridConnectionsWebSocketServer';

const hyCoWebSocket = require('hyco-websocket');
const nodeHybridConnectionsWebSocketServer = hyCoWebSocket.relayedServer;

export function createHybridRpcServerProvider(relayUrl: string, relaySas: string | undefined) {
    const onRpcStreamJoinedEmitter = new rpc.Emitter<ssh.Stream>();
    const onCloseEmitter = new rpc.Emitter<Error>();
    const onErrorEmitter = new rpc.Emitter<Error>();

    const serverUrl = SshHelpers.getRelayUri(relayUrl, relaySas, 'listen');
    return {
        name: 'hybrid',
        onRpcStreamJoined: onRpcStreamJoinedEmitter.event,
        onClose: onCloseEmitter.event,
        onError: onErrorEmitter.event,
        acceptConnections(cancellationToken?: rpc.CancellationToken) {
            if (isNode()) {
                const hc = new nodeHybridConnectionsWebSocketServer({
                    server: serverUrl,
                    token: relaySas,
                    autoAcceptConnections: true,
                });
                hc.on('connect', (connection: WebSocketConnection) => {
                    console.log(
                        `Azure relay provider accepting connections on serverUrl:${serverUrl}`,
                    );
                    onRpcStreamJoinedEmitter.fire(
                        SshHelpers.createWebSocketStreamAdapter(connection),
                    );
                });
                hc.on(
                    'close',
                    (connection: WebSocketConnection, closeReason: number, description: string) => {
                        onCloseEmitter.fire(new Error(description));
                    },
                );
            } else {
                const hc = new HybridConnectionsWebSocketServer(
                    {
                        server: serverUrl,
                    },
                    (ws) =>
                        SshHelpers.webSshStreamFactory(ws).then((s) =>
                            onRpcStreamJoinedEmitter.fire(s),
                        ),
                );
                hc.on('error', (error) => onErrorEmitter.fire(error));
                hc.on('close', (error) => onCloseEmitter.fire(error));
            }

            return Promise.resolve();
        },
    };
}
