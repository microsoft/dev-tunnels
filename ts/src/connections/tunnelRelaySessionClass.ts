// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelAccessScopes } from '@vs/tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelAccessTokenProperties } from '@vs/tunnels-management';
import { Stream, TraceLevel } from '@vs/vs-ssh';
import { TunnelRelayStreamFactory, DefaultTunnelRelayStreamFactory } from '.';
import { TunnelSession } from './tunnelSession';

type Constructor<T = {}> = new (...args: any[]) => T;

/**
 * Tunnel relay mixin class that adds relay connection capability to descendants of TunnelSession.
 * @param base Base class constructor.
 * @param webSocketSubProtocol Web socket sub-protocol.
 * @returns A class where createSessionStream() connects to tunnel relay.
 */
export function tunnelRelaySessionClass<TBase extends Constructor<TunnelSession>>(
    base: TBase,
    webSocketSubProtocol: string,
) {
    return class TunnelRelaySession extends base {
        /**
         * Web socket sub-protocol to connect to the tunnel relay endpoint.
         */
        public static readonly webSocketSubProtocol = webSocketSubProtocol;

        /**
         * Tunnel relay URI.
         */
        public relayUri?: string;

        /**
         * Gets or sets a factory for creating relay streams.
         */
        public streamFactory: TunnelRelayStreamFactory = new DefaultTunnelRelayStreamFactory();

        /**
         * Creates a stream to the tunnel.
         */
        public async createSessionStream(cancellation: CancellationToken): Promise<Stream> {
            if (!this.relayUri) {
                throw new Error(
                    'Cannot create tunnel session stream. Tunnel relay endpoint URI is missing',
                );
            }

            const name = this.tunnelAccessScope === TunnelAccessScopes.Connect ? 'client' : 'host';
            const accessToken = this.validateAccessToken();
            this.trace(TraceLevel.Info, 0, `Connecting to ${name} tunnel relay ${this.relayUri}`);
            this.trace(TraceLevel.Verbose, 0, `Sec-WebSocket-Protocol: ${webSocketSubProtocol}`);
            if (accessToken) {
                const tokenTrace = TunnelAccessTokenProperties.getTokenTrace(accessToken);
                this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel <${tokenTrace}>`);
            }

            return await this.streamFactory.createRelayStream(
                this.relayUri,
                webSocketSubProtocol,
                accessToken,
            );
        }

        /**
         * Connect to the tunnel session with the tunnel connector.
         * @param tunnel Tunnel to use for the connection.
         *     Undefined if the connection information is already known and the tunnel is not needed.
         *     Tunnel object to get the connection information from that tunnel.
         */
        public async connectTunnelSession(tunnel?: Tunnel): Promise<void> {
            try {
                await super.connectTunnelSession(tunnel);
            } catch (ex) {
                throw new Error('Failed to connect to tunnel relay. ' + ex);
            }
        }

        /**
         * Validate the tunnel and get data needed to connect to it, if the tunnel is provided;
         * otherwise, ensure that there is already sufficient data to connect to a tunnel.
         * @param tunnel Tunnel to use for the connection.
         *     Tunnel object to get the connection data if defined.
         *     Undefined if the connection data is already known.
         */
        public async onConnectingToTunnel(tunnel?: Tunnel): Promise<void> {
            await super.onConnectingToTunnel(tunnel);
            if (!this.relayUri) {
                this.relayUri = await this.getTunnelRelayUri(tunnel);
                if (!this.relayUri) {
                    throw new Error('The tunnel relay endpoint URI is missing.');
                }
            }
        }

        /**
         * Gets the tunnel relay URI.
         */
        public async getTunnelRelayUri(tunnel?: Tunnel): Promise<string> {
            throw new Error('getTunnelRelayUri() is not implemented');
        }
    };
}
