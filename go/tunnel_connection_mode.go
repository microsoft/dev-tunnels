// Generated from ../../../cs/src/Contracts/TunnelConnectionMode.cs

package tunnels

// Specifies the connection protocol / implementation for a tunnel.
//
// Depending on the connection mode, hosts or clients might need to use different
// authentication and connection protocols.
type TunnelConnectionMode string

const (
	// Connect directly to the host over the local network.
	//
	// While it's technically not "tunneling", this mode may be combined with others to
	// enable choosing the most efficient connection mode available.
	TunnelConnectionModeLocalNetwork   TunnelConnectionMode = "localNetwork"

	// Use the tunnel service's integrated relay function.
	TunnelConnectionModeTunnelRelay    TunnelConnectionMode = "tunnelRelay"

	// Connect via a Live Share workspace's Azure Relay endpoint.
	TunnelConnectionModeLiveShareRelay TunnelConnectionMode = "liveShareRelay"
)
