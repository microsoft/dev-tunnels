// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel } from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { CancellationToken } from '@microsoft/dev-tunnels-ssh';

/**
 * Event args for tunnel refresh event.
 */
export class RefreshingTunnelEventArgs {
    /**
     * Creates a new instance of RefreshingTunnelAccessTokenEventArgs class.
     */
    public constructor(
        /**
         * Tunnel access scope to get the token for.
         */
        public readonly tunnelAccessScope: string,

        /**
         * Tunnel being refreshed.
         */
        public readonly tunnel: Tunnel | null,

        /**
         * A value indicating whether ports need to be included into the refreshed tunnel.
         */
        public readonly includePorts: boolean,

        /**
         * Management client used for connections.
         */
        public readonly managementClient?: TunnelManagementClient,


        /**
         * Cancellation token that event handler may observe when it asynchronously fetches the tunnel access token.
         */
        public readonly cancellation?: CancellationToken,
    ) {}

    /**
     * Tunnel promise the event handler may set to asynchnronously fetch the tunnel.
     * The result of the promise may be a new tunnel or null if it couldn't get the tunnel.
     */
    public tunnelPromise?: Promise<Tunnel | null>;
}
