// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelConnectionMode, Tunnel, TunnelRelayTunnelEndpoint } from '@vs/tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelManagementClient } from '@vs/tunnels-management';
import { Stream, Trace } from '@vs/vs-ssh';
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

    constructor(trace?: Trace, managementClient?: TunnelManagementClient) {
        super(trace, managementClient);
    }

    /**
     * Gets the tunnel relay URI.
     */
    public async getTunnelRelayUri(tunnel?: Tunnel): Promise<string> {
        let tunnelEndpoints = this.endpoints!.map(
            (endpoint) => endpoint as TunnelRelayTunnelEndpoint,
        );
        let tunnelEndpoint;
        if (tunnelEndpoints && tunnelEndpoints.length === 1) {
            tunnelEndpoint = tunnelEndpoints[0];
        } else {
            throw new Error('The host is not currently accepting Tunnel relay connections.');
        }

        return tunnelEndpoint.clientRelayUri!;
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
