// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/Tunnel.cs

package tunnels

import (
	"time"
)

// Data contract for tunnel objects managed through the tunnel service REST API.
type Tunnel struct {
	// Gets or sets the ID of the cluster the tunnel was created in.
	ClusterID        string `json:"clusterId,omitempty"`

	// Gets or sets the generated ID of the tunnel, unique within the cluster.
	TunnelID         string `json:"tunnelId,omitempty"`

	// Gets or sets the optional short name (alias) of the tunnel.
	//
	// The name must be globally unique within the parent domain, and must be a valid
	// subdomain.
	Name             string `json:"name,omitempty"`

	// Gets or sets the description of the tunnel.
	Description      string `json:"description,omitempty"`

	// Gets or sets the tags of the tunnel.
	Tags             []string `json:"tags,omitempty"`

	// Gets or sets the optional parent domain of the tunnel, if it is not using the default
	// parent domain.
	Domain           string `json:"domain,omitempty"`

	// Gets or sets a dictionary mapping from scopes to tunnel access tokens.
	AccessTokens     map[TunnelAccessScope]string `json:"accessTokens,omitempty"`

	// Gets or sets access control settings for the tunnel.
	//
	// See `TunnelAccessControl` documentation for details about the access control model.
	AccessControl    *TunnelAccessControl `json:"accessControl,omitempty"`

	// Gets or sets default options for the tunnel.
	Options          *TunnelOptions `json:"options,omitempty"`

	// Gets or sets current connection status of the tunnel.
	Status           *TunnelStatus `json:"status,omitempty"`

	// Gets or sets an array of endpoints where hosts are currently accepting client
	// connections to the tunnel.
	Endpoints        []TunnelEndpoint `json:"endpoints,omitempty"`

	// Gets or sets a list of ports in the tunnel.
	//
	// This optional property enables getting info about all ports in a tunnel at the same
	// time as getting tunnel info, or creating one or more ports at the same time as
	// creating a tunnel. It is omitted when listing (multiple) tunnels, or when updating
	// tunnel properties. (For the latter, use APIs to create/update/delete individual ports
	// instead.)
	Ports            []TunnelPort `json:"ports,omitempty"`

	// Gets or sets the time in UTC of tunnel creation.
	Created          *time.Time `json:"created,omitempty"`

	// Gets or the time the tunnel will be deleted if it is not used or updated.
	Expiration       *time.Time `json:"expiration,omitempty"`

	// Gets or the custom amount of time the tunnel will be valid if it is not used or
	// updated in seconds.
	CustomExpiration uint32 `json:"customExpiration,omitempty"`
}
