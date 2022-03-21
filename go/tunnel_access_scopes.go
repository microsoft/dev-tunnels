// Generated from ../../../cs/src/Contracts/TunnelAccessScopes.cs

package tunnels

// Defines scopes for tunnel access tokens.
type TunnelAccessScopes []TunnelAccessScope
type TunnelAccessScope string

const (
	// Allows management operations on tunnels and tunnel ports.
	TunnelAccessScopeManage  TunnelAccessScope = "manage"

	// Allows accepting connections on tunnels as a host.
	TunnelAccessScopeHost    TunnelAccessScope = "host"

	// Allows inspecting tunnel connection activity and data.
	TunnelAccessScopeInspect TunnelAccessScope = "inspect"

	// Allows connecting to tunnels as a client.
	TunnelAccessScopeConnect TunnelAccessScope = "connect"
)
