// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelAccessScopes } from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelAccessTokenProperties } from '@microsoft/dev-tunnels-management';
import { Stream, TraceLevel } from '@microsoft/dev-tunnels-ssh';
import { TunnelRelayStreamFactory, DefaultTunnelRelayStreamFactory } from '.';
import { TunnelSession } from './tunnelSession';
import { IClientConfig } from 'websocket';
import * as http from 'http';


type Constructor<T = object> = new (...args: any[]) => T;

/**
 * Tunnel relay mixin class that adds relay connection capability to descendants of TunnelSession.
 * @param base Base class constructor.
 * @param protocols Web socket sub-protocols.
 * @returns A class where createSessionStream() connects to tunnel relay.
 */
export function tunnelRelaySessionClass<TBase extends Constructor<TunnelSession>>(
    base: TBase,
    protocols: string[],
) {
    return class TunnelRelaySession extends base {
        /**
         * Tunnel relay URI.
         * @internal
         */
        public relayUri?: string;

        /**
         * Gets or sets a factory for creating relay streams.
         */
        public streamFactory: TunnelRelayStreamFactory = new DefaultTunnelRelayStreamFactory();

        /**
         * Creates a stream to the tunnel.
         * @internal
         */
        public async createSessionStream(cancellation: CancellationToken): Promise<{ stream: Stream, protocol: string }> {
            if (!this.relayUri) {
                throw new Error(
                    'Cannot create tunnel session stream. Tunnel relay endpoint URI is missing',
                );
            }

            const name = this.tunnelAccessScope === TunnelAccessScopes.Connect ? 'client' : 'host';
            const accessToken = this.validateAccessToken();
            this.trace(TraceLevel.Info, 0, `Connecting to ${name} tunnel relay ${this.relayUri}`);
            this.trace(TraceLevel.Verbose, 0, `Sec-WebSocket-Protocol: ${protocols.join(', ')}`);
            if (accessToken) {
                const tokenTrace = TunnelAccessTokenProperties.getTokenTrace(accessToken);
                this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel <${tokenTrace}>`);
            }

            const clientConfig: IClientConfig = {
                tlsOptions: {
                    agent: this.httpAgent,
                },
            };

            const streamAndProtocol = await this.streamFactory.createRelayStream(
                this.relayUri,
                protocols,
                accessToken,
                clientConfig
            );

            this.trace(
                TraceLevel.Verbose,
                0,
                `Connected with subprotocol '${streamAndProtocol.protocol}'`);
            return streamAndProtocol;
        }

        /**
         * Connect to the tunnel session with the tunnel connector.
         * @param tunnel Tunnel to use for the connection.
         *     Undefined if the connection information is already known and the tunnel is not needed.
         *     Tunnel object to get the connection information from that tunnel.
         * @internal
         */
        public async connectTunnelSession(tunnel?: Tunnel, httpAgent?: http.Agent): Promise<void> {
            try {
                await super.connectTunnelSession(tunnel, httpAgent);
            } catch (ex) {
                throw new Error('Failed to connect to tunnel relay. ' + ex);
            }
        }
    };
}
