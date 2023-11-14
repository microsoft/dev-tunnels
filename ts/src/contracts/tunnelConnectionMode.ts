// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConnectionMode.cs
/* eslint-disable */

/**
 * Specifies the connection protocol / implementation for a tunnel.
 *
 * Depending on the connection mode, hosts or clients might need to use different
 * authentication and connection protocols.
 */
export enum TunnelConnectionMode {
    /**
     * Connect directly to the host over the local network.
     *
     * While it's technically not "tunneling", this mode may be combined with others to
     * enable choosing the most efficient connection mode available.
     */
    LocalNetwork = 'LocalNetwork',

    /**
     * Use the tunnel service's integrated relay function.
     */
    TunnelRelay = 'TunnelRelay',
}
