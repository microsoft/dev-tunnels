// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { SshClientSession, Trace } from '@vs/vs-ssh';
import { SshHelpers } from './sshHelpers';
import { TunnelClientBase } from './tunnelClientBase';
import {
    Tunnel,
    TunnelConnectionMode,
    TunnelEndpoint,
    LiveShareRelayTunnelEndpoint,
} from '@vs/tunnels-contracts';

/**
 * Tunnel client implementation that connects via a Live Share session's Azure Relay.
 */
export class LiveShareRelayTunnelClient extends TunnelClientBase {
    public sshSession?: SshClientSession;
    public connectionModes = [TunnelConnectionMode.LiveShareRelay];
    public trace: Trace = (level, eventId, msg, err) => {};

    constructor() {
        super();
    }

    public async connectClient(tunnel: Tunnel, endpoints: TunnelEndpoint[]): Promise<void> {
        let liveShareEndpoints = endpoints.map(
            (endpoint) => endpoint as LiveShareRelayTunnelEndpoint,
        );
        let liveShareEndpoint;
        if (liveShareEndpoints && liveShareEndpoints.length === 1) {
            liveShareEndpoint = liveShareEndpoints[0];
        } else {
            throw new Error('The host is not currently accepting Live Share relay connections.');
        }

        let relayUri = liveShareEndpoint.relayUri;
        if (!relayUri) {
            throw new Error('The Live Share relay endpoint URI is missing.');
        }

        const url = SshHelpers.getRelayUri(
            relayUri,
            liveShareEndpoint.relayClientSasToken,
            'connect',
        );
        try {
            const stream = await SshHelpers.openConnection(url);
            await this.startSshSession(stream);
        } catch (ex) {
            console.log(ex);
        }
    }
}
