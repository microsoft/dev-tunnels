// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelAccessScopes } from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelAccessTokenProperties } from '@microsoft/dev-tunnels-management';
import { Stream, TraceLevel } from '@microsoft/dev-tunnels-ssh';
import { TunnelRelayStreamFactory, DefaultTunnelRelayStreamFactory } from '.';
import { TunnelSession } from './tunnelSession';

type Constructor<T = object> = new (...args: any[]) => T;

/**
 * Tunnel relay mixin class that adds relay connection capability to descendants of TunnelSession.
 * @param base Base class constructor.
 * @param webSocketSubProtocol Web socket sub-protocol.
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
        public async createSessionStream(
            cancellation: CancellationToken,
        ): Promise<{ stream: Stream, protocol: string }> {
            if (!this.relayUri) {
                throw new Error(
                    'Cannot create tunnel session stream. Tunnel relay endpoint URI is missing',
                );
            }

            const name = this.tunnelAccessScope === TunnelAccessScopes.Connect ? 'client' : 'host';
            const accessToken = this.validateAccessToken();
            this.trace(TraceLevel.Info, 0, `Connecting to ${name} tunnel relay ${this.relayUri}`);
            this.trace(TraceLevel.Verbose, 0, `Requesting subprotocol(s): ${protocols.join(', ')}`);
            if (accessToken) {
                const tokenTrace = TunnelAccessTokenProperties.getTokenTrace(accessToken);
                this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel <${tokenTrace}>`);
            }

            const streamAndProtocol = await this.streamFactory.createRelayStream(
                this.relayUri,
                protocols,
                accessToken,
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
         * @internal
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
         * @internal
         */
        public async getTunnelRelayUri(tunnel?: Tunnel): Promise<string> {
            throw new Error('getTunnelRelayUri() is not implemented');
        }
    };
}
