// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

'use strict';

import * as events from 'events';

const isDefinedAndNonNull = (options: any, key: string) =>
    typeof options[key] !== 'undefined' && options[key] !== null;

/**
 * WebSocket Server implementation
 */
export class HybridConnectionsWebSocketServer extends events.EventEmitter {
    private listenUri: any;
    private closeRequested: boolean;
    private options: any;
    private clients: WebSocket[];
    private controlChannel: WebSocket | undefined;
    constructor(options: any, private readonly callback: (websocket: WebSocket) => void) {
        super();

        options = Object.assign(
            {
                server: null,
                token: null,
                verifyClient: null,
                handleProtocols: null,
                disableHixie: false,
                clientTracking: true,
                perMessageDeflate: true,
                maxPayload: 100 * 1024 * 1024,
                backlog: null, // use default (511 as implemented in net.js)
            },
            options,
        );

        if (!isDefinedAndNonNull(options, 'server')) {
            throw new TypeError('server must be provided');
        }

        this.listenUri = options.server;

        this.closeRequested = false;
        this.options = options;
        this.clients = [];

        this.connectControlChannel();
    }

    /**
     * Immediately shuts down the connection.
     *
     * @api public
     */
    public close(callback: any) {
        this.closeRequested = true;
        // terminate all associated clients
        let error = null;
        try {
            for (let i = 0, l = this.clients.length; i < l; ++i) {
                this.clients[i].close();
            }
            this.controlChannel?.close();
        } catch (e) {
            error = e;
        }

        if (callback) {
            callback(error);
        } else if (error) {
            throw error;
        }
    }

    private connectControlChannel() {
        /* create the control connection */
        this.controlChannel = new WebSocket(this.listenUri);

        this.controlChannel.onerror = (event) => {
            this.emit('error', event);
            if (!this.closeRequested) {
                this.connectControlChannel();
            }
        };

        this.controlChannel.onopen = (event) => {
            this.emit('listening');
            console.log(
                `Azure relay provider accepting connections on serverUrl:${this.options.server}`,
            );
        };

        this.controlChannel.onclose = (event) => {
            if (!this.closeRequested) {
                // reconnect
                this.connectControlChannel();
            } else {
                this.emit('close', this);
            }
        };

        this.controlChannel.onmessage = (event) => {
            const message = JSON.parse(event.data);
            if (isDefinedAndNonNull(message, 'accept')) {
                this.accept(message);
            }
        };
    }

    private accept(message: any) {
        console.log(`accept -> message:${JSON.stringify(message)}`);

        const address = message.accept.address;
        const req = { headers: {} };

        for (let keys = Object.keys(message.accept.connectHeaders), l = keys.length; l; --l) {
            // @ts-ignore
            req.headers[keys[l - 1].toLowerCase()] = message.accept.connectHeaders[keys[l - 1]];
        }
        // verify key presence
        // @ts-ignore
        if (!req.headers['sec-websocket-key']) {
            this.abortConnection(message, 400, 'Bad Request');
            return;
        }

        // verify protocol
        // @ts-ignore
        const protocols = req.headers['sec-websocket-protocol'];

        // handler to call when the connection sequence completes
        const self = this;
        const completeHybridUpgrade = (proto: string[] | undefined) => {
            try {
                const client = new WebSocket(address, proto);

                client.onerror = (event) => {
                    const index = self.clients.indexOf(client);
                    if (index !== -1) {
                        self.clients.splice(index, 1);
                    }
                };

                self.callback(client);

                if (self.options.clientTracking) {
                    self.clients.push(client);
                    client.onclose = () => {
                        const index = self.clients.indexOf(client);
                        if (index !== -1) {
                            self.clients.splice(index, 1);
                        }
                    };
                }
            } catch (err) {
                console.log(`completeHybridUpgrade failed`, err);
            }
        };

        if (typeof protocols !== 'undefined') {
            completeHybridUpgrade(protocols.split(/, */)[0]);
        } else {
            completeHybridUpgrade(undefined);
        }
    }

    private abortConnection(
        message: { address: string },
        status: string | number,
        reason: string | number | boolean,
    ) {
        const rejectUri =
            message.address +
            '&statusCode=' +
            status +
            '&statusDescription=' +
            encodeURIComponent(reason);
        const client = new WebSocket(rejectUri);

        client.onerror = (connection) => {
            this.emit('requestRejected', this);
        };
    }
}
