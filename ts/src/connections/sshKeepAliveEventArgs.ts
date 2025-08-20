// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Event args raised when an SSH keep-alive succeeds or fails.
 */
export class SshKeepAliveEventArgs {
    /**
     * The number of keep-alive messages that have been sent with the same state.
     */
    public readonly count: number;

    public constructor(count: number) {
        this.count = count;
    }
}
