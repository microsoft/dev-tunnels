// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { SshStream } from '@microsoft/dev-tunnels-ssh';

/**
 * Forwarded port connecting event args.
 */
export class ForwardedPortConnectingEventArgs {
    /**
     * Creates a new instance of ForwardedPortConnectingEventArgs.
     */
    public constructor(
        /**
         * Gets the forwarded port
         */
        public readonly port: number,

        /**
         * Gets the stream
         */
        public readonly stream: SshStream,
    ) {}
}
