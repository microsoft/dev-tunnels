// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { LiveShareRelayTunnelEndpoint, Tunnel, TunnelAccessScopes } from '@vs/tunnels-contracts';
import { TunnelManagementClient, TunnelRequestOptions } from '@vs/tunnels-management';

/**
 * Provides Azure Relay access tokens by querying them via a tunnel management client.
 */
export class LiveShareRelayTokenProvider {
    private initialToken: string | undefined;
    private tunnel: Tunnel;
    private hostId: string;
    private tokenScope: string;
    private managementClient: TunnelManagementClient;

    constructor(
        managementClient: TunnelManagementClient,
        tunnel: Tunnel,
        hostId: string,
        tokenScope: string,
        initialToken: string | undefined = undefined,
    ) {
        if (!managementClient || !tunnel || !hostId || !tokenScope) {
            throw new Error('Cannot initialize LiveShareTokenProvider with undefined values');
        }

        this.managementClient = managementClient;
        this.tunnel = tunnel;
        this.hostId = hostId;
        this.tokenScope = tokenScope;
        this.initialToken = initialToken;
    }

    public async onGetTokenAsync(audience: string, validFor: number): Promise<any> {
        let sasToken = null;
        if (this.initialToken) {
            sasToken = this.initialToken;
            this.initialToken = undefined;
        } else {
            // TODO: Refresh the tunnel access token first if necessary.

            let tunnelAccessToken = this.tunnel.accessTokens![this.tokenScope];

            let options: TunnelRequestOptions = {
                accessToken: tunnelAccessToken,
                tokenScopes: [this.tokenScope],
            };
            let refreshedTunnel = await this.managementClient.getTunnel(this.tunnel, options);

            if (refreshedTunnel) {
                let endpoints = refreshedTunnel.endpoints
                    ?.filter((end) => (end.hostId = this.hostId))
                    .map((e) => e as LiveShareRelayTunnelEndpoint);
                let endpoint = endpoints && endpoints.length > 0 ? endpoints[0] : undefined;
                if (endpoint) {
                    sasToken =
                        this.tokenScope === TunnelAccessScopes.Host
                            ? endpoint.relayHostSasToken
                            : endpoint.relayClientSasToken;
                }

                if (!sasToken) {
                    throw new Error('Relay token was not returned by service.');
                }
            }
        }

        //let sasTokenProvider = CreateSharedAccessSignatureTokenProvider(sasToken);
        return undefined; //await sasTokenProvider.GetTokenAsync(audience, validFor);
    }
}
