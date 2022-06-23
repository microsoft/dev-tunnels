// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs

// Defines scopes for tunnel access tokens.

// Allows creating tunnels. This scope is valid only in policies at the global, domain, or
// organization level; it is not relevant to an already-created tunnel or tunnel port.
// (Creation of ports requires "manage" or "host" access to the tunnel.)
pub const TUNNEL_ACCESS_SCOPES_CREATE: &str = "create";

// Allows management operations on tunnels and tunnel ports.
pub const TUNNEL_ACCESS_SCOPES_MANAGE: &str = "manage";

// Allows accepting connections on tunnels as a host.
pub const TUNNEL_ACCESS_SCOPES_HOST: &str = "host";

// Allows inspecting tunnel connection activity and data.
pub const TUNNEL_ACCESS_SCOPES_INSPECT: &str = "inspect";

// Allows connecting to tunnels as a client.
pub const TUNNEL_ACCESS_SCOPES_CONNECT: &str = "connect";
