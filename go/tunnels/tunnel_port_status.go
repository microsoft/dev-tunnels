// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortStatus.cs

package tunnels

import (
	"time"
)

// Data contract for `TunnelPort` status.
type TunnelPortStatus struct {
	// Gets or sets the current value and limit for the number of clients connected to the
	// port.
	//
	// This client connection count does not include non-port-specific connections such as
	// SDK and SSH clients. See `TunnelStatus.ClientConnectionCount` for status of those
	// connections.  This count also does not include HTTP client connections, unless they
	// are upgraded to websockets. HTTP connections are counted per-request rather than
	// per-connection: see `TunnelPortStatus.HttpRequestRate`.
	ClientConnectionCount    *ResourceStatus `json:"clientConnectionCount,omitempty"`

	// Gets or sets the UTC date time when a client was last connected to the port, or null
	// if a client has never connected.
	LastClientConnectionTime *time.Time `json:"lastClientConnectionTime,omitempty"`

	// Gets or sets the current value and limit for the rate of client connections to the
	// tunnel port.
	//
	// This client connection rate does not count non-port-specific connections such as SDK
	// and SSH clients. See `TunnelStatus.ClientConnectionRate` for those connection types.
	// This also does not include HTTP connections, unless they are upgraded to websockets.
	// HTTP connections are counted per-request rather than per-connection: see
	// `TunnelPortStatus.HttpRequestRate`.
	ClientConnectionRate     *RateStatus `json:"clientConnectionRate,omitempty"`

	// Gets or sets the current value and limit for the rate of HTTP requests to the tunnel
	// port.
	HttpRequestRate          *RateStatus `json:"httpRequestRate,omitempty"`
}
