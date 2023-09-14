// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortV2.cs

package tunnels

// Data contract for tunnel port objects managed through the tunnel service REST API.
type TunnelPortV2 struct {
	// Gets or sets the ID of the cluster the tunnel was created in.
	ClusterID          string `json:"clusterId,omitempty"`

	// Gets or sets the generated ID of the tunnel, unique within the cluster.
	TunnelID           string `json:"tunnelId,omitempty"`

	// Gets or sets the IP port number of the tunnel port.
	PortNumber         uint16 `json:"portNumber"`

	// Gets or sets the optional short name of the port.
	//
	// The name must be unique among named ports of the same tunnel.
	Name               string `json:"name,omitempty"`

	// Gets or sets the optional description of the port.
	Description        string `json:"description,omitempty"`

	// Gets or sets the tags of the port.
	Labels             []string `json:"labels,omitempty"`

	// Gets or sets the protocol of the tunnel port.
	//
	// Should be one of the string constants from `TunnelProtocol`.
	Protocol           string `json:"protocol,omitempty"`

	// Gets or sets a value indicating whether this port is a default port for the tunnel.
	//
	// A client that connects to a tunnel (by ID or name) without specifying a port number
	// will connect to the default port for the tunnel, if a default is configured. Or if the
	// tunnel has only one port then the single port is the implicit default.
	// 
	// Selection of a default port for a connection also depends on matching the connection
	// to the port `TunnelPortV2.Protocol`, so it is possible to configure separate defaults
	// for distinct protocols like `TunnelProtocol.Http` and `TunnelProtocol.Ssh`.
	IsDefault          bool `json:"isDefault,omitempty"`

	// Gets or sets a dictionary mapping from scopes to tunnel access tokens.
	//
	// Unlike the tokens in `Tunnel.AccessTokens`, these tokens are restricted to the
	// individual port.
	AccessTokens       map[TunnelAccessScope]string `json:"accessTokens,omitempty"`

	// Gets or sets access control settings for the tunnel port.
	//
	// See `TunnelAccessControl` documentation for details about the access control model.
	AccessControl      *TunnelAccessControl `json:"accessControl,omitempty"`

	// Gets or sets options for the tunnel port.
	Options            *TunnelOptions `json:"options,omitempty"`

	// Gets or sets current connection status of the tunnel port.
	Status             *TunnelPortStatus `json:"status,omitempty"`

	// Gets or sets the username for the ssh service user is trying to forward.
	//
	// Should be provided if the `TunnelProtocol` is Ssh.
	SshUser            string `json:"sshUser,omitempty"`

	// Gets or sets web forwarding URIs. If set, it's a list of absolute URIs where the port
	// can be accessed with web forwarding.
	PortForwardingURIs []string `json:"portForwardingUris"`

	// Gets or sets inspection URI. If set, it's an absolute URIs where the port's traffic
	// can be inspected.
	InspectionURI      string `json:"inspectionUri"`
}
