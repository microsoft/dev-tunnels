// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs
/* eslint-disable */

import { TunnelEndpoint } from './tunnelEndpoint';

/**
 * Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
 */
export interface TunnelRelayTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets the host URI.
     */
    hostRelayUri?: string;

    /**
     * Gets or sets the client URI.
     */
    clientRelayUri?: string;
}
