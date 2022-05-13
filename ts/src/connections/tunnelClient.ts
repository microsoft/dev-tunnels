// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelConnectionMode, Tunnel } from '@vs/tunnels-contracts';
import { SshStream, CancellationToken } from '@vs/vs-ssh';
import { ForwardedPortsCollection } from '@vs/vs-ssh-tcp';
import { Disposable } from 'vscode-jsonrpc';

/**
 * Interface for a client capable of making a connection
 * to a tunnel and forwarding ports over the tunnel.
 */
export interface TunnelClient extends Disposable {
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
}
