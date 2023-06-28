// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationToken } from '@microsoft/dev-tunnels-ssh';

/**
 * Tunnel connector.
 */
export interface TunnelConnector {
    /**
     * Connect or reconnect tunnel SSH session.
     * @param isReconnect A value indicating if this is a reconnect (true) or regular connect (false).
     * @param cancellation Cancellation token.
     */
    connectSession(isReconnect: boolean, cancellation: CancellationToken): Promise<void>;
}
