// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelStatus.cs
/* eslint-disable */

import { RateStatus } from './rateStatus';
import { ResourceStatus } from './resourceStatus';

/**
 * Data contract for {@link Tunnel} status.
 */
export interface TunnelStatus {
    /**
     * Gets or sets the current value and limit for the number of ports on the tunnel.
     */
    portCount?: number | ResourceStatus;

    /**
     * Gets or sets the current value and limit for the number of hosts currently
     * accepting connections to the tunnel.
     *
     * This is typically 0 or 1, but may be more than 1 if the tunnel options allow
     * multiple hosts.
     */
    hostConnectionCount?: number | ResourceStatus;

    /**
     * Gets or sets the UTC time when a host was last accepting connections to the tunnel,
     * or null if a host has never connected.
     */
    lastHostConnectionTime?: Date;

    /**
     * Gets or sets the current value and limit for the number of clients connected to the
     * tunnel.
     *
     * This counts non-port-specific client connections, which is SDK and SSH clients. See
     * {@link TunnelPortStatus} for status of per-port client connections.
     */
    clientConnectionCount?: number | ResourceStatus;

    /**
     * Gets or sets the UTC time when a client last connected to the tunnel, or null if a
     * client has never connected.
     *
     * This reports times for non-port-specific client connections, which is SDK client
     * and SSH clients. See {@link TunnelPortStatus} for per-port client connections.
     */
    lastClientConnectionTime?: Date;

    /**
     * Gets or sets the current value and limit for the rate of client connections to the
     * tunnel.
     *
     * This counts non-port-specific client connections, which is SDK client and SSH
     * clients. See {@link TunnelPortStatus} for status of per-port client connections.
     */
    clientConnectionRate?: RateStatus;

    /**
     * Gets or sets the current value and limit for the rate of bytes transferred via the
     * tunnel.
     *
     * This includes both sending and receiving. All types of tunnel and port connections
     * contribute to this rate.
     */
    dataTransferRate?: RateStatus;

    /**
     * Gets or sets the current value and limit for the rate of management API read
     * operations  for the tunnel or tunnel ports.
     */
    apiReadRate?: RateStatus;

    /**
     * Gets or sets the current value and limit for the rate of management API update
     * operations for the tunnel or tunnel ports.
     */
    apiUpdateRate?: RateStatus;
}
