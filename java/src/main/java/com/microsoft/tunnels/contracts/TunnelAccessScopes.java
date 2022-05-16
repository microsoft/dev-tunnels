// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelAccessScopes.cs

package com.microsoft.tunnels.contracts;

/**
 * Defines scopes for tunnel access tokens.
 */
public class TunnelAccessScopes {
    /**
     * Allows creating tunnels. This scope is valid only in policies at the global,
     * domain, or organization level; it is not relevant to an already-created tunnel or
     * tunnel port. (Creation of ports requires "manage" or "host" access to the tunnel.)
     */
    public static final String create = "create";

    /**
     * Allows management operations on tunnels and tunnel ports.
     */
    public static final String manage = "manage";

    /**
     * Allows accepting connections on tunnels as a host.
     */
    public static final String host = "host";

    /**
     * Allows inspecting tunnel connection activity and data.
     */
    public static final String inspect = "inspect";

    /**
     * Allows connecting to tunnels as a client.
     */
    public static final String connect = "connect";
}
