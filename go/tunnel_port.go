// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPort.cs

package tunnels

// Data contract for tunnel port objects managed through the tunnel service REST API.
type TunnelPort struct {
	// Gets or sets the ID of the cluster the tunnel was created in.
	ClusterID string `json:"clusterId,omitempty"`

	// Gets or sets the generated ID of the tunnel, unique within the cluster.
	TunnelID string `json:"tunnelId,omitempty"`

	// Gets or sets the IP port number of the tunnel port.
	PortNumber uint16 `json:"portNumber"`

	// Gets or sets the protocol of the tunnel port.
	//
	// Should be one of the string constants from `TunnelProtocol`.
	Protocol string `json:"protocol,omitempty"`

	// Gets or sets a dictionary mapping from scopes to tunnel access tokens.
	//
	// Unlike the tokens in `Tunnel.AccessTokens`, these tokens are restricted to the
	// individual port.
	AccessTokens map[TunnelAccessScope]string `json:"accessTokens,omitempty"`

	// Gets or sets access control settings for the tunnel port.
	//
	// See `TunnelAccessControl` documentation for details about the access control model.
	AccessControl *TunnelAccessControl `json:"accessControl,omitempty"`

	// Gets or sets options for the tunnel port.
	Options *TunnelOptions `json:"options,omitempty"`

	// Gets or sets current connection status of the tunnel port.
	Status *TunnelPortStatus `json:"status,omitempty"`
}
