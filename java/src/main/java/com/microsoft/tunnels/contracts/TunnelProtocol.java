// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelProtocol.cs

package com.microsoft.tunnels.contracts;

/**
 * Defines possible values for the protocol of a {@link TunnelPort}.
 */
public class TunnelProtocol {
    /**
     * The protocol is automatically detected. (TODO: Define detection semantics.)
     */
    public static final String auto = "auto";

    /**
     * Unknown TCP protocol.
     */
    public static final String tcp = "tcp";

    /**
     * Unknown UDP protocol.
     */
    public static final String udp = "udp";

    /**
     * SSH protocol.
     */
    public static final String ssh = "ssh";

    /**
     * Remote desktop protocol.
     */
    public static final String rdp = "rdp";

    /**
     * HTTP protocol.
     */
    public static final String http = "http";

    /**
     * HTTPS protocol.
     */
    public static final String https = "https";
}
