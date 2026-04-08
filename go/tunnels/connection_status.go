// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

// ConnectionStatus represents the connection state of a tunnel host.
type ConnectionStatus int

const (
	// ConnectionStatusNone indicates no connection has been made.
	ConnectionStatusNone ConnectionStatus = iota

	// ConnectionStatusConnecting indicates a connection is in progress.
	ConnectionStatusConnecting

	// ConnectionStatusConnected indicates the host is connected.
	ConnectionStatusConnected

	// ConnectionStatusDisconnected indicates the host has disconnected.
	ConnectionStatusDisconnected
)

// String returns the string representation of the connection status.
func (s ConnectionStatus) String() string {
	switch s {
	case ConnectionStatusNone:
		return "None"
	case ConnectionStatusConnecting:
		return "Connecting"
	case ConnectionStatusConnected:
		return "Connected"
	case ConnectionStatusDisconnected:
		return "Disconnected"
	default:
		return "Unknown"
	}
}
