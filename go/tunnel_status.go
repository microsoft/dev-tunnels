// Generated from ../../../cs/src/Contracts/TunnelStatus.cs

package tunnels

import (
	"time"
)

// Data contract for `Tunnel` status.
type TunnelStatus struct {
	// Gets or sets the number of hosts currently accepting connections to the tunnel.
	//
	// This is typically 0 or 1, but may be more than 1 if the tunnel options allow multiple
	// hosts.
	HostConnectionCount      uint32 `json:"hostConnectionCount"`

	// Gets or sets the UTC time when a host was last accepting connections to the tunnel, or
	// null if a host has never connected.
	LastHostConnectionTime   time.Time `json:"lastHostConnectionTime,omitempty"`

	// Gets or sets the number of clients currently connected to the tunnel.
	ClientConnectionCount    uint32 `json:"clientConnectionCount"`

	// Gets or sets the UTC time when a client last connected to the tunnel, or null if a
	// client has never connected.
	LastClientConnectionTime time.Time `json:"lastClientConnectionTime"`
}
