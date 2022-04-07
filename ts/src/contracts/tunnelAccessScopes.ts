// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs
/* eslint-disable */

/**
 * Defines scopes for tunnel access tokens.
 */
export enum TunnelAccessScopes {
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
