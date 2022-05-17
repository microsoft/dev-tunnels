// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs

package goTunnels

// Defines scopes for tunnel access tokens.
type TunnelAccessScopes []TunnelAccessScope
type TunnelAccessScope string

const (
	// Allows creating tunnels. This scope is valid only in policies at the global, domain,
	// or organization level; it is not relevant to an already-created tunnel or tunnel port.
	// (Creation of ports requires "manage" or "host" access to the tunnel.)
	TunnelAccessScopeCreate TunnelAccessScope = "create"

	// Allows management operations on tunnels and tunnel ports.
	TunnelAccessScopeManage TunnelAccessScope = "manage"

	// Allows accepting connections on tunnels as a host.
	TunnelAccessScopeHost TunnelAccessScope = "host"

	// Allows inspecting tunnel connection activity and data.
	TunnelAccessScopeInspect TunnelAccessScope = "inspect"

	// Allows connecting to tunnels as a client.
	TunnelAccessScopeConnect TunnelAccessScope = "connect"
)
