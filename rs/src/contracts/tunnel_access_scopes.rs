// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs

// Defines scopes for tunnel access tokens.

// Allows creating tunnels. This scope is valid only in policies at the global, domain, or
// organization level; it is not relevant to an already-created tunnel or tunnel port.
// (Creation of ports requires "manage" or "host" access to the tunnel.)
const CREATE: &str = "create";

// Allows management operations on tunnels and tunnel ports.
const MANAGE: &str = "manage";

// Allows accepting connections on tunnels as a host.
const HOST: &str = "host";

// Allows inspecting tunnel connection activity and data.
const INSPECT: &str = "inspect";

// Allows connecting to tunnels as a client.
const CONNECT: &str = "connect";
