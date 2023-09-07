// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelPort } from '@microsoft/dev-tunnels-contracts';
import { TunnelConnection } from './tunnelConnection';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import { CancellationToken } from '@microsoft/dev-tunnels-ssh';

/**
 * Interface for a host capable of sharing local ports via
 * a tunnel and accepting tunneled connections to those ports.
 */
export interface TunnelHost extends TunnelConnection {
    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     * @deprecated Use `connect()` instead. 
     */
    start(tunnel: Tunnel): Promise<void>;

    /**
     * Connects to a tunnel as a host and starts accepting incoming connections
     * to local ports as defined on the tunnel.
     * 
     * The host either needs to be logged in as the owner identity, or have
     * an access token with "host" scope for the tunnel.
     * 
     * @param tunnel Tunnel to connect to.
     * @param options Options for the connection.
     * @param cancellation Optional cancellation token for the connection.
     */
    connect(
        tunnel: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<void>;

    /**
     * Refreshes ports that were updated using the management API.
     *
     * After using the management API to add or remove ports, call this method to have the
     * host update its cached list of ports. Any added or removed ports will then propagate to
     * the set of ports forwarded by all connected clients.
     */
    refreshPorts(): Promise<void>;
}
