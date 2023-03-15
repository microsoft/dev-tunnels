// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Event args for tunnel connection retry event.
 */
export class RetryingTunnelConnectionEventArgs {
    public constructor(
        /**
         * Gets the error that caused the retry.
         */
        public readonly error: Error,

        /**
         * Gets the amount of time to wait before retrying. An event handler may change this value.
         */
        public delayMs: number,
    ) {}

    /**
     * Gets or sets a value indicating whether the retry will proceed. An event handler may
     * set this to false to stop retrying.
     */
    public retry: boolean = true;
}
