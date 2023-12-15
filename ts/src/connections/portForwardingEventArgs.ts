// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Event raised when a port is about to be forwarded to the tunnel client.
 */
export class PortForwardingEventArgs {
    /**
     * Creates a new instance of PortForwardingEventArgs.
     */
    public constructor(
        /**
         * Gets the port number that is being forwarded.
         */
        public readonly portNumber: number,
    ) {}

    public cancel: boolean = false;
}
