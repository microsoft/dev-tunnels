// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Duplex } from 'stream';
import { Event } from 'vscode-jsonrpc';
import { TunnelConnectionMode, Tunnel } from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from '@microsoft/dev-tunnels-ssh';
import { ForwardedPortsCollection } from '@microsoft/dev-tunnels-ssh-tcp';
import { TunnelConnection } from './tunnelConnection';
import { TunnelConnectionOptions } from './tunnelConnectionOptions';
import { PortForwardingEventArgs } from './portForwardingEventArgs';

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
     * Event raised when a port is about to be forwarded to the client.
     *
     * The application may cancel this event to prevent specific port(s) from being
     * forwarded to the client. Cancelling prevents the tunnel client from listening on
     * a local socket for the port, AND prevents use of {@link connectToForwardedPort}
     * to open a direct stream connection to the port. This event is still fired when
     * {@link acceptLocalConnectionsForForwardedPorts} is false.
     */
    readonly portForwarding: Event<PortForwardingEventArgs>;

    /**
     * Gets list of ports forwarded to client, this collection
     * contains events to notify when ports are forwarded
     */
    readonly forwardedPorts: ForwardedPortsCollection | undefined;

    /**
     * Gets a value indicating whether local connections for forwarded ports are accepted.
     * Local connections are not accepted if the host process is not NodeJS (e.g. browser).
     * Default: true for NodeJS, false for browser.
     */
    acceptLocalConnectionsForForwardedPorts: boolean;

    /**
     * Gets or sets the local network interface address that the tunnel client listens on when
     * accepting connections for forwarded ports. The default value is the loopback address
     * (127.0.0.1). Applications may set this to the address indicating any interface (0.0.0.0)
     * or to the address of a specific interface. The tunnel client supports both IPv4 and IPv6
     * when listening on either loopback or any interface.
     */
    localForwardingHostAddress: string;

    /**
     * Connects to a tunnel.
     * 
     * Once connected, tunnel ports are forwarded by the host.
     * The client either needs to be logged in as the owner identity, or have
     * an access token with "connect" scope for the tunnel.
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
    ): Promise<Duplex>;

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
