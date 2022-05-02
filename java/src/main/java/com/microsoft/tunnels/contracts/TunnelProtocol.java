// Generated from ../../../../../../../../cs/src/Contracts/TunnelProtocol.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Defines possible values for the protocol of a {@link TunnelPort}.
 */
public class TunnelProtocol {
    /**
     * The protocol is automatically detected. (TODO: Define detection semantics.)
     */
    @Expose
    public static String auto = "auto";

    /**
     * Unknown TCP protocol.
     */
    @Expose
    public static String tcp = "tcp";

    /**
     * Unknown UDP protocol.
     */
    @Expose
    public static String udp = "udp";

    /**
     * SSH protocol.
     */
    @Expose
    public static String ssh = "ssh";

    /**
     * Remote desktop protocol.
     */
    @Expose
    public static String rdp = "rdp";

    /**
     * HTTP protocol.
     */
    @Expose
    public static String http = "http";

    /**
     * HTTPS protocol.
     */
    @Expose
    public static String https = "https";
}
