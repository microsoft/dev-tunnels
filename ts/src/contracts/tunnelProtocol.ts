// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelProtocol.cs
/* eslint-disable */

/**
 * Defines possible values for the protocol of a {@link TunnelPort}.
 */
export enum TunnelProtocol {
    /**
     * The protocol is automatically detected. (TODO: Define detection semantics.)
     */
    Auto = 'auto',

    /**
     * Unknown TCP protocol.
     */
    Tcp = 'tcp',

    /**
     * Unknown UDP protocol.
     */
    Udp = 'udp',

    /**
     * SSH protocol.
     */
    Ssh = 'ssh',

    /**
     * Remote desktop protocol.
     */
    Rdp = 'rdp',

    /**
     * HTTP protocol.
     */
    Http = 'http',

    /**
     * HTTPS protocol.
     */
    Https = 'https',
}
