// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortStatus.cs

package tunnels

import (
	"time"
)

// Data contract for `TunnelPort` status.
type TunnelPortStatus struct {
	// Gets or sets the number of clients currently connected to the port.
	//
	// The client connection count does not include the host. (See the
	// `TunnelStatus.HostConnectionCount` property for host connection status. Hosts always
	// listen for incoming connections on all tunnel ports simultaneously.)
	ClientConnectionCount    uint32 `json:"clientConnectionCount"`

	// Gets or sets the UTC date time when a client was last connected to the port.
	LastClientConnectionTime *time.Time `json:"lastClientConnectionTime,omitempty"`
}
