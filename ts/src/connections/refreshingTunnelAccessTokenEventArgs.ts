// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationToken, SshDisconnectReason, Stream, Trace } from '@vs/vs-ssh';

/**
 * Event args for tunnel access token refresh event.
 */
export class RefreshingTunnelAccessTokenEventArgs {
    /**
     * Creates a new instance of RefreshingTunnelAccessTokenEventArgs class.
     */
    constructor(
        /**
         * Tunnel access scope to get the token for.
         */
        public readonly tunnelAccessScope: string,

        /**
         * Cancellation token that event handler may observe when it asynchronously fetches the tunnel access token.
         */
        public readonly cancellation: CancellationToken,
    ) {}

    /**
     * Token promise the event handler may set to asynchnronously fetch the token.
     * The result of the promise may be a new tunnel access token or undefined if it couldn't get the token.
     */
    public tunnelAccessToken?: Promise<string | undefined>;
}
