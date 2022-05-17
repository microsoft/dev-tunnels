// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelStatus.cs

package tunnels

import (
	"time"
)

// Data contract for `Tunnel` status.
type TunnelStatus struct {
	// Gets or sets the current value and limit for the number of ports on the tunnel.
	PortCount *ResourceStatus `json:"portCount,omitempty"`

	// Gets or sets the current value and limit for the number of hosts currently accepting
	// connections to the tunnel.
	//
	// This is typically 0 or 1, but may be more than 1 if the tunnel options allow multiple
	// hosts.
	HostConnectionCount *ResourceStatus `json:"hostConnectionCount,omitempty"`

	// Gets or sets the UTC time when a host was last accepting connections to the tunnel, or
	// null if a host has never connected.
	LastHostConnectionTime *time.Time `json:"lastHostConnectionTime,omitempty"`

	// Gets or sets the current value and limit for the number of clients connected to the
	// tunnel.
	//
	// This counts non-port-specific client connections, which is SDK and SSH clients. See
	// `TunnelPortStatus` for status of per-port client connections.
	ClientConnectionCount *ResourceStatus `json:"clientConnectionCount,omitempty"`

	// Gets or sets the UTC time when a client last connected to the tunnel, or null if a
	// client has never connected.
	//
	// This reports times for non-port-specific client connections, which is SDK client and
	// SSH clients. See `TunnelPortStatus` for per-port client connections.
	LastClientConnectionTime *time.Time `json:"lastClientConnectionTime,omitempty"`

	// Gets or sets the current value and limit for the rate of client connections to the
	// tunnel.
	//
	// This counts non-port-specific client connections, which is SDK client and SSH clients.
	// See `TunnelPortStatus` for status of per-port client connections.
	ClientConnectionRate *RateStatus `json:"clientConnectionRate,omitempty"`

	// Gets or sets the current value and limit for the rate of bytes transferred via the
	// tunnel.
	//
	// This includes both sending and receiving. All types of tunnel and port connections
	// contribute to this rate.
	DataTransferRate *RateStatus `json:"dataTransferRate,omitempty"`

	// Gets or sets the current value and limit for the rate of management API read
	// operations  for the tunnel or tunnel ports.
	ApiReadRate *RateStatus `json:"apiReadRate,omitempty"`

	// Gets or sets the current value and limit for the rate of management API update
	// operations for the tunnel or tunnel ports.
	ApiUpdateRate *RateStatus `json:"apiUpdateRate,omitempty"`
}
