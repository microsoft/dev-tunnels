// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelConnectionMode, Tunnel } from '@vs/tunnels-contracts';
import { SshStream, CancellationToken } from '@microsoft/dev-tunnels-ssh';
import { ForwardedPortsCollection } from '@microsoft/dev-tunnels-ssh-tcp';
import { TunnelConnection } from './tunnelConnection';

/**
 * Interface for a client capable of making a connection
 * to a tunnel and forwarding ports over the tunnel.
 */
export interface TunnelClient extends TunnelConnection {
    /**
     * Gets the list of connection modes that this client supports.
     */
    readonly connectionModes: TunnelConnectionMode[];

    /**
     * Gets list of ports forwarded to client, this collection
     * contains events to notify when ports are forwarded
     */
    readonly forwardedPorts: ForwardedPortsCollection | undefined;

    /**
     * A value indicating whether local connections for forwarded ports are accepted.
     * Local connections are not accepted if the host process is not NodeJS (e.g. browser).
     * Default: true for NodeJS, false for browser.
     */
    acceptLocalConnectionsForForwardedPorts: boolean;

    /**
     * Connects to a tunnel.
     * @param tunnel
     * @param hostId
     */
    connect(tunnel: Tunnel, hostId?: string): Promise<void>;

    /**
     * Opens a stream connected to a remote port for clients which cannot forward local TCP ports, such as browsers.
     *
     * This method should only be called after {@link connect}. Calling {@link waitForForwardedPort}
     * before {@link connectToForwardedPort} may also be necessary in case the port is not yet available.
     *
     * @param fowardedPort Remote port to connect to.
     * @param cancellation Optional cancellation token for the request.
     * @returns A stream that is relayed to the remote port.
     */
    connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<SshStream>;

    /**
     * Waits for the specified port to be forwarded by the remote host.
     *
     * Call before {@link connectToForwardedPort} to ensure that a forwarded port is available before attempting to connect.
     *
     * @param forwardedPort Remote port to wait for.
     * @param cancellation Optional cancellation for the request.
     */
    waitForForwardedPort(forwardedPort: number, cancellation?: CancellationToken): Promise<void>;

    /**
     * Sends a request to the host to refresh ports that were updated using the management API,
     * and waits for the refresh to complete.
     *
     * After using the management API to add or remove ports, call this method to have a
     * connected client notify the host to update its cached list of ports. Any added or
     * removed ports will then propagate back to the set of ports forwarded by the current
     * client. After the returned task has completed, any newly added ports are usable from
     * the current client.
     */
    refreshPorts(): Promise<void>;
}
