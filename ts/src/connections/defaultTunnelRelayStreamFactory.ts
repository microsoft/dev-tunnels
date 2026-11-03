// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Stream } from '@microsoft/dev-tunnels-ssh';
import { TunnelRelayStreamFactory } from './tunnelRelayStreamFactory';
import { isNode, SshHelpers } from './sshHelpers';
import { IClientConfig } from 'websocket';

/**
 * Request header that a host sends to the relay to identify its own process.
 *
 * The value is the host's ID, captured from `MultiModeTunnelHost.hostId` when the host was
 * constructed, and is the same value the host publishes as `TunnelEndpoint.hostId`. It lets
 * the relay recognize a host that is reconnecting to a tunnel it already holds, rather than
 * treating it as a different host taking the tunnel over. Clients do not send this header.
 */
const hostIdHeaderName = 'X-Tunnels-Host-Process-Id';

// Mirrors the host sub-protocols in tunnelRelayTunnelHost.ts. They are duplicated here rather
// than imported because that module reaches back to this one through the connection session.
const hostSubProtocols = ['tunnel-relay-host', 'tunnel-relay-host-v2-dev'];

/**
 * Default factory for creating streams to a tunnel relay.
 */
export class DefaultTunnelRelayStreamFactory implements TunnelRelayStreamFactory {
    public async createRelayStream(
        relayUri: string,
        protocols: string[],
        accessToken?: string,
        clientConfig?: IClientConfig,
        additionalHeaders?: { [header: string]: string },
        hostId?: string,
    ): Promise<{ stream: Stream, protocol: string }> {
        if (isNode()) {
            const isHostConnection = protocols.some((p) => hostSubProtocols.includes(p));
            const stream = await SshHelpers.openConnection(
                relayUri,
                protocols,
                {
                    ...additionalHeaders,
                    ...(accessToken && { Authorization: `tunnel ${accessToken}` }),
                    ...(isHostConnection &&
                        hostId && {
                            [hostIdHeaderName]: hostId,
                        }),
                },
                clientConfig,
            );
            return { stream, protocol: stream.protocol! };
        } else {
            // Browser WebSocket APIs cannot set auth or custom handshake headers.
            if (accessToken) {
                protocols = [...protocols, accessToken];
            }
            const stream = await SshHelpers.openConnection(relayUri, protocols);
            return { stream, protocol: stream.protocol! };
        }
    }
}
