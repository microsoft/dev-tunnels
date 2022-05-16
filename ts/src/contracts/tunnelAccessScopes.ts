// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs
/* eslint-disable */

/**
 * Defines scopes for tunnel access tokens.
 */
export enum TunnelAccessScopes {
    /**
     * Allows creating tunnels. This scope is valid only in policies at the global,
     * domain, or organization level; it is not relevant to an already-created tunnel or
     * tunnel port. (Creation of ports requires "manage" or "host" access to the tunnel.)
     */
    Create = 'create',

    /**
     * Allows management operations on tunnels and tunnel ports.
     */
    Manage = 'manage',

    /**
     * Allows accepting connections on tunnels as a host.
     */
    Host = 'host',

    /**
     * Allows inspecting tunnel connection activity and data.
     */
    Inspect = 'inspect',

    /**
     * Allows connecting to tunnels as a client.
     */
    Connect = 'connect',
}
