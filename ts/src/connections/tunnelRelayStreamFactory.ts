// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Stream } from '@microsoft/dev-tunnels-ssh';
import { IClientConfig } from 'websocket';

/**
 * Interface for a factory capable of creating streams to a tunnel relay.
 */
export interface TunnelRelayStreamFactory {
    /**
     * Creates a stream connected to a tunnel relay URI.
     * @param relayUri URI of the tunnel relay to connect to.
     * @param protocols Array of supported connection protocols (websocket sub-protocols).
     * @param accessToken Tunnel host access token, or null if anonymous.
     * @param clientConfig Client config for websocket.
     * @param hostId ID of the connecting host process, sent to the relay as the
     * `X-Tunnels-Host-Process-Id` header, or undefined for client connections and for hosts
     * that have no ID to advertise.
     */
    createRelayStream(
        relayUri: string,
        protocols: string[],
        accessToken?: string,
        clientConfig?: IClientConfig,
        hostId?: string,
    ): Promise<{ stream: Stream, protocol: string }>;
}
