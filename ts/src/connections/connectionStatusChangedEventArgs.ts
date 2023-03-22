// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { ConnectionStatus } from './connectionStatus';

/**
 * Connection status changed event args.
 */
export class ConnectionStatusChangedEventArgs {
    /**
     * Creates a new instance of ConnectionStatusChangedEventArgs.
     */
    public constructor(
        /**
         * Gets the previous connection status.
         */
        public readonly previousStatus: ConnectionStatus,

        /**
         * Gets the current connection status.
         */
        public readonly status: ConnectionStatus,

        /**
         * Gets the error that caused disconnect if {@link status} is {@link ConnectionStatus.Disconnected}.
         */
        public readonly disconnectError?: Error,
    ) {}
}
