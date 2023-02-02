// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs

// Defines scopes for tunnel access tokens.
//
// A tunnel access token with one or more of these scopes typically also has cluster ID
// and tunnel ID claims that limit the access scope to a specific tunnel, and may also
// have one or more port claims that further limit the access to particular ports of the
// tunnel.

// Allows creating tunnels. This scope is valid only in policies at the global, domain, or
// organization level; it is not relevant to an already-created tunnel or tunnel port.
// (Creation of ports requires "manage" or "host" access to the tunnel.)
pub const TUNNEL_ACCESS_SCOPES_CREATE: &str = r#"create"#;

// Allows management operations on tunnels and tunnel ports.
pub const TUNNEL_ACCESS_SCOPES_MANAGE: &str = r#"manage"#;

// Allows management operations on all ports of a tunnel, but does not allow updating any
// other tunnel properties or deleting the tunnel.
pub const TUNNEL_ACCESS_SCOPES_MANAGE_PORTS: &str = r#"manage:ports"#;

// Allows accepting connections on tunnels as a host. Includes access to update tunnel
// endpoints and ports.
pub const TUNNEL_ACCESS_SCOPES_HOST: &str = r#"host"#;

// Allows inspecting tunnel connection activity and data.
pub const TUNNEL_ACCESS_SCOPES_INSPECT: &str = r#"inspect"#;

// Allows connecting to tunnels or ports as a client.
pub const TUNNEL_ACCESS_SCOPES_CONNECT: &str = r#"connect"#;
