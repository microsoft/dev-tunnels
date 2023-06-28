// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelConnectionMode,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { Stream, Trace } from '@microsoft/dev-tunnels-ssh';
import { TunnelClientBase, webSocketSubProtocol, webSocketSubProtocolv2 } from './tunnelClientBase';
import { tunnelRelaySessionClass } from './tunnelRelaySessionClass';

// Check for an environment variable to determine which protocol version to use.
// By default, prefer V2 and fall back to V1.
const protocolVersion = process?.env && process.env.DEVTUNNELS_PROTOCOL_VERSION;
const connectionProtocols =
    protocolVersion === '1' ? [webSocketSubProtocol] :
    protocolVersion === '2' ? [webSocketSubProtocolv2] :
    [webSocketSubProtocolv2, webSocketSubProtocol];

/**
 * Tunnel client implementation that connects via a tunnel relay.
 */
export class TunnelRelayTunnelClient extends tunnelRelaySessionClass(
    TunnelClientBase,
    connectionProtocols,
) {
    public static readonly webSocketSubProtocol = webSocketSubProtocol;
    public static readonly webSocketSubProtocolv2 = webSocketSubProtocolv2;

    public connectionModes: TunnelConnectionMode[] = [];

    public constructor(trace?: Trace, managementClient?: TunnelManagementClient) {
        super(trace, managementClient);
    }

    protected tunnelChanged() {
        super.tunnelChanged();
        if (!this.tunnel) {
            this.relayUri = undefined;
        } else {
            if (!this.endpoints || this.endpoints.length === 0) {
                throw new Error('No hosts are currently accepting connections for the tunnel.');
            }
            const tunnelEndpoints: TunnelRelayTunnelEndpoint[] = this.endpoints.filter(
                (ep) => ep.connectionMode === TunnelConnectionMode.TunnelRelay,
            );
    
            if (tunnelEndpoints.length === 0) {
                throw new Error('The host is not currently accepting Tunnel relay connections.');
            }
    
            // TODO: What if there are multiple relay endpoints, which one should the tunnel client pick, or is this an error?
            // For now, just chose the first one.
            const endpoint = tunnelEndpoints[0];
            this.hostPublicKeys = endpoint.hostPublicKeys;
            this.relayUri = endpoint.clientRelayUri!;
        }
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
        protocol: string,
        isReconnect: boolean,
        cancellation: CancellationToken,
    ): Promise<void> {
        this.connectionProtocol = protocol;
        if (isReconnect && this.sshSession && !this.sshSession.isClosed) {
            await this.sshSession.reconnect(stream, cancellation);
        } else {
            await this.startSshSession(stream, cancellation);
        }
    }
}
