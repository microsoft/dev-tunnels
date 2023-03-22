// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelConnectionMode,
    Tunnel,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { Stream, Trace } from '@microsoft/dev-tunnels-ssh';
import { TunnelClientBase } from './tunnelClientBase';
import { tunnelRelaySessionClass } from './tunnelRelaySessionClass';

const webSocketSubProtocol = 'tunnel-relay-client';

/**
 * Tunnel client implementation that connects via a tunnel relay.
 */
export class TunnelRelayTunnelClient extends tunnelRelaySessionClass(
    TunnelClientBase,
    webSocketSubProtocol,
) {
    public connectionModes: TunnelConnectionMode[] = [];

    public constructor(trace?: Trace, managementClient?: TunnelManagementClient) {
        super(trace, managementClient);
    }

    /**
     * Gets the tunnel relay URI.
     * @internal
     */
    public async getTunnelRelayUri(tunnel?: Tunnel): Promise<string> {
        if (!this.endpoints || this.endpoints.length === 0) {
            throw new Error('No hosts are currently accepting connections for the tunnel.');
        }
        const tunnelEndpoints: TunnelRelayTunnelEndpoint[] = this.endpoints.filter(
            (endpoint) => endpoint.connectionMode === TunnelConnectionMode.TunnelRelay,
        );

        if (tunnelEndpoints.length === 0) {
            throw new Error('The host is not currently accepting Tunnel relay connections.');
        }

        // TODO: What if there are multiple relay endpoints, which one should the tunnel client pick, or is this an error?
        // For now, just chose the first one.
        return tunnelEndpoints[0].clientRelayUri!;
    }

    /**
     * Connect to the tunnel session on the relay service using the given access token for authorization.
     */
    protected async connectClientToRelayServer(
        clientRelayUri: string,
        accessToken?: string,
    ): Promise<void> {
        if (!clientRelayUri) {
            throw new Error('Client relay URI must be a non-empty string');
        }

        this.relayUri = clientRelayUri;
        this.accessToken = accessToken;
        await this.connectTunnelSession();
    }

    /**
     * Configures the tunnel session with the given stream.
     * @internal
     */
    public async configureSession(
        stream: Stream,
        isReconnect: boolean,
        cancellation: CancellationToken,
    ): Promise<void> {
        if (isReconnect && this.sshSession && !this.sshSession.isClosed) {
            await this.sshSession.reconnect(stream, cancellation);
        } else {
            await this.startSshSession(stream, cancellation);
        }
    }
}
