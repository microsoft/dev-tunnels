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
	PortCount                *ResourceStatus `json:"portCount,omitempty"`

	// Gets or sets the current value and limit for the number of hosts currently accepting
	// connections to the tunnel.
	//
	// This is typically 0 or 1, but may be more than 1 if the tunnel options allow multiple
	// hosts.
	HostConnectionCount      *ResourceStatus `json:"hostConnectionCount,omitempty"`

	// Gets or sets the UTC time when a host was last accepting connections to the tunnel, or
	// null if a host has never connected.
	LastHostConnectionTime   *time.Time `json:"lastHostConnectionTime,omitempty"`

	// Gets or sets the current value and limit for the number of clients connected to the
	// tunnel.
	//
	// This counts non-port-specific client connections, which is SDK and SSH clients. See
	// `TunnelPortStatus` for status of per-port client connections.
	ClientConnectionCount    *ResourceStatus `json:"clientConnectionCount,omitempty"`

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
	ClientConnectionRate     *RateStatus `json:"clientConnectionRate,omitempty"`

	// Gets or sets the current value and limit for the rate of bytes being received by the
	// tunnel host and uploaded by tunnel clients.
	//
	// All types of tunnel and port connections, from potentially multiple clients, can
	// contribute to this rate. The reported rate may differ slightly from the rate
	// measurable by applications, due to protocol overhead. Data rate status reporting is
	// delayed by a few seconds, so this value is a snapshot of the data transfer rate from a
	// few seconds earlier.
	UploadRate               *RateStatus `json:"uploadRate,omitempty"`

	// Gets or sets the current value and limit for the rate of bytes being sent by the
	// tunnel host and downloaded by tunnel clients.
	//
	// All types of tunnel and port connections, from potentially multiple clients, can
	// contribute to this rate. The reported rate may differ slightly from the rate
	// measurable by applications, due to protocol overhead. Data rate status reporting is
	// delayed by a few seconds, so this value is a snapshot of the data transfer rate from a
	// few seconds earlier.
	DownloadRate             *RateStatus `json:"downloadRate,omitempty"`

	// Gets or sets the total number of bytes received by the tunnel host and uploaded by
	// tunnel clients, over the lifetime of the tunnel.
	//
	// All types of tunnel and port connections, from potentially multiple clients, can
	// contribute to this total. The reported value may differ slightly from the value
	// measurable by applications, due to protocol overhead. Data transfer status reporting
	// is delayed by a few seconds.
	UploadTotal              uint64 `json:"uploadTotal,omitempty"`

	// Gets or sets the total number of bytes sent by the tunnel host and downloaded by
	// tunnel clients, over the lifetime of the tunnel.
	//
	// All types of tunnel and port connections, from potentially multiple clients, can
	// contribute to this total. The reported value may differ slightly from the value
	// measurable by applications, due to protocol overhead. Data transfer status reporting
	// is delayed by a few seconds.
	DownloadTotal            uint64 `json:"downloadTotal,omitempty"`

	// Gets or sets the current value and limit for the rate of management API read
	// operations  for the tunnel or tunnel ports.
	ApiReadRate              *RateStatus `json:"apiReadRate,omitempty"`

	// Gets or sets the current value and limit for the rate of management API update
	// operations for the tunnel or tunnel ports.
	ApiUpdateRate            *RateStatus `json:"apiUpdateRate,omitempty"`
}
