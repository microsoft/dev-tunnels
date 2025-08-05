// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelEvent.cs

package tunnels

import (
	"time"
)

// Data contract for tunnel client events reported to the tunnel service.
type TunnelEvent struct {
	// Gets or sets the UTC timestamp of the event (using the client's clock).
	Timestamp  *time.Time `json:"timestamp,omitempty"`

	// Gets or sets name of the event. This should be a short descriptive identifier.
	Name       string `json:"name"`

	// Gets or sets the severity of the event, such as `TunnelEvent.Info`,
	// `TunnelEvent.Warning`, or `TunnelEvent.Error`.
	//
	// If not specified, the default severity is "info".
	Severity   string `json:"severity,omitempty"`

	// Gets or sets optional unstructured details about the event, such as a message or
	// description. For warning or error events this may include a stack trace.
	Details    string `json:"details,omitempty"`

	// Gets or sets semi-structured event properties.
	Properties map[string]string `json:"properties,omitempty"`
}

// Default event severity.
var Info = "info"

// Warning event severity.
var Warning = "warning"

// Error event severity.
var Error = "error"
