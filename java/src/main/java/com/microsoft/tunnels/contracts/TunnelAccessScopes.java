// Generated from ../../../../../../../../cs/src/Contracts/TunnelAccessScopes.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Defines scopes for tunnel access tokens.
 */
public class TunnelAccessScopes {
    /**
     * Allows creating tunnels. This scope is valid only in policies at the global,
     * domain, or organization level; it is not relevant to an already-created tunnel or
     * tunnel port. (Creation of ports requires "manage" or "host" access to the tunnel.)
     */
    @Expose
    public static String create = "create";

    /**
     * Allows management operations on tunnels and tunnel ports.
     */
    @Expose
    public static String manage = "manage";

    /**
     * Allows accepting connections on tunnels as a host.
     */
    @Expose
    public static String host = "host";

    /**
     * Allows inspecting tunnel connection activity and data.
     */
    @Expose
    public static String inspect = "inspect";

    /**
     * Allows connecting to tunnels as a client.
     */
    @Expose
    public static String connect = "connect";
}
