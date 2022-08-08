// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    TunnelConnectionMode,
    Tunnel,
    TunnelEndpoint,
    TunnelRelayTunnelEndpoint,
    TunnelAccessScopes,
} from '@vs/tunnels-contracts';
import { TunnelAccessTokenProperties } from '@vs/tunnels-management';
import { TraceLevel } from '@vs/vs-ssh';
import { TunnelRelayStreamFactory, DefaultTunnelRelayStreamFactory } from '.';
import { TunnelClientBase } from './tunnelClientBase';

/**
 * Tunnel client implementation that connects via a tunnel relay.
 */
export class TunnelRelayTunnelClient extends TunnelClientBase {
    /**
     * Web socket sub-protocol to connect to the tunnel relay endpoint.
     */
    public static webSocketSubProtocol = 'tunnel-relay-client';
    public connectionModes: TunnelConnectionMode[] = [];

    /**
     * Gets or sets a factory for creating relay streams.
     */
    public streamFactory: TunnelRelayStreamFactory = new DefaultTunnelRelayStreamFactory();

    constructor() {
        super();
    }

    public async connectClient(tunnel: Tunnel, endpoints: TunnelEndpoint[]): Promise<void> {
        let tunnelEndpoints = endpoints.map((endpoint) => endpoint as TunnelRelayTunnelEndpoint);
        let tunnelEndpoint;
        if (tunnelEndpoints && tunnelEndpoints.length === 1) {
            tunnelEndpoint = tunnelEndpoints[0];
        } else {
            throw new Error('The host is not currently accepting Tunnel relay connections.');
        }

        let clientRelayUri = tunnelEndpoint.clientRelayUri;
        if (!clientRelayUri) {
            throw new Error('The tunnel client relay endpoint URI is missing.');
        }

        let accessToken = tunnel.accessTokens
            ? tunnel.accessTokens[TunnelAccessScopes.Connect]
            : undefined;

        await this.connectClientToRelayServer(clientRelayUri, accessToken);
    }

    protected async connectClientToRelayServer(
        clientRelayUri: string,
        accessToken?: string,
    ): Promise<void> {
        this.trace(TraceLevel.Info, 0, `Connecting to client tunnel relay ${clientRelayUri}`);
        this.trace(
            TraceLevel.Verbose,
            0,
            `Sec-WebSocket-Protocol: ${TunnelRelayTunnelClient.webSocketSubProtocol}`,
        );
        if (accessToken) {
            const token = TunnelAccessTokenProperties.tryParse(accessToken)?.toString() ?? 'token';
            this.trace(TraceLevel.Verbose, 0, `Authorization: tunnel <${token}>`);
        }

        try {
            let stream = await this.streamFactory.createRelayStream(
                clientRelayUri,
                TunnelRelayTunnelClient.webSocketSubProtocol,
                accessToken,
            );
            try {
                await this.startSshSession(stream);
            } catch {
                stream.dispose();
                throw new Error();
            }
        } catch (ex) {
            throw new Error('Failed to connect to tunnel relay. ' + ex);
        }
    }
}
