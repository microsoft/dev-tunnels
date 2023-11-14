// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortStatus.cs
/* eslint-disable */

import { RateStatus } from './rateStatus';
import { ResourceStatus } from './resourceStatus';

/**
 * Data contract for {@link TunnelPort} status.
 */
export interface TunnelPortStatus {
    /**
     * Gets or sets the current value and limit for the number of clients connected to the
     * port.
     *
     * This client connection count does not include non-port-specific connections such as
     * SDK and SSH clients. See {@link TunnelStatus.clientConnectionCount} for status of
     * those connections.  This count also does not include HTTP client connections,
     * unless they are upgraded to websockets. HTTP connections are counted per-request
     * rather than per-connection: see {@link TunnelPortStatus.httpRequestRate}.
     */
    clientConnectionCount?: number | ResourceStatus;

    /**
     * Gets or sets the UTC date time when a client was last connected to the port, or
     * null if a client has never connected.
     */
    lastClientConnectionTime?: Date;

    /**
     * Gets or sets the current value and limit for the rate of client connections to the
     * tunnel port.
     *
     * This client connection rate does not count non-port-specific connections such as
     * SDK and SSH clients. See {@link TunnelStatus.clientConnectionRate} for those
     * connection types.  This also does not include HTTP connections, unless they are
     * upgraded to websockets. HTTP connections are counted per-request rather than
     * per-connection: see {@link TunnelPortStatus.httpRequestRate}.
     */
    clientConnectionRate?: RateStatus;

    /**
     * Gets or sets the current value and limit for the rate of HTTP requests to the
     * tunnel port.
     */
    httpRequestRate?: RateStatus;
}
