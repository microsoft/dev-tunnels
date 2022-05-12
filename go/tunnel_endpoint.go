// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelEndpoint.cs

package tunnels

// Base class for tunnel connection parameters.
//
// A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
// There is a subclass for each connection mode, each having different connection
// parameters. A tunnel may have multiple endpoints for one host (or multiple hosts), and
// clients can select their preferred endpoint(s) from those depending on network
// environment or client capabilities.
type TunnelEndpoint struct {
	// Gets or sets the connection mode of the endpoint.
	//
	// This property is required when creating or updating an endpoint.  The subclass type is
	// also an indication of the connection mode, but this property is necessary to determine
	// the subclass type when deserializing.
	ConnectionMode TunnelConnectionMode `json:"connectionMode"`

	// Gets or sets the ID of the host that is listening on this endpoint.
	//
	// This property is required when creating or updating an endpoint.  If the host supports
	// multiple connection modes, the host's ID is the same for all the endpoints it
	// supports. However different hosts may simultaneously accept connections at different
	// endpoints for the same tunnel, if enabled in tunnel options.
	HostID string `json:"hostId"`

	// Gets or sets an array of public keys, which can be used by clients to authenticate the
	// host.
	HostPublicKeys []string `json:"hostPublicKeys,omitempty"`

	// Gets or sets a string used to format URIs where a web client can connect to ports of
	// the tunnel. The string includes a `TunnelEndpoint.PortUriToken` that must be replaced
	// with the actual port number.
	PortURIFormat string `json:"portUriFormat,omitempty"`

	LiveShareRelayTunnelEndpoint
	LocalNetworkTunnelEndpoint
	TunnelRelayTunnelEndpoint
}

// Parameters for connecting to a tunnel via a Live Share Azure Relay.
type LiveShareRelayTunnelEndpoint struct {
	// Gets or sets the Live Share workspace ID.
	WorkspaceID string `json:"workspaceId"`

	// Gets or sets the Azure Relay URI.
	RelayURI string `json:"relayUri,omitempty"`

	// Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
	RelayHostSasToken string `json:"relayHostSasToken,omitempty"`

	// Gets or sets a SAS token that allows clients to connect to the Azure Relay endpoint.
	RelayClientSasToken string `json:"relayClientSasToken,omitempty"`
}

// Parameters for connecting to a tunnel via a local network connection.
//
// While a direct connection is technically not "tunneling", tunnel hosts may accept
// connections via the local network as an optional more-efficient alternative to a relay.
type LocalNetworkTunnelEndpoint struct {
	// Gets or sets a list of IP endpoints where the host may accept connections.
	//
	// A host may accept connections on multiple IP endpoints simultaneously if there are
	// multiple network interfaces on the host system and/or if the host supports both IPv4
	// and IPv6.  Each item in the list is a URI consisting of a scheme (which gives an
	// indication of the network connection protocol), an IP address (IPv4 or IPv6) and a
	// port number. The URIs do not typically include any paths, because the connection is
	// not normally HTTP-based.
	HostEndpoints []string `json:"hostEndpoints"`
}

// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
type TunnelRelayTunnelEndpoint struct {
	// Gets or sets the host URI.
	HostRelayURI string `json:"hostRelayUri,omitempty"`

	// Gets or sets the client URI.
	ClientRelayURI string `json:"clientRelayUri,omitempty"`
}

// Token included in `TunnelEndpoint.PortUriFormat` that is to be replaced by a specified
// port number.
var PortURIToken = "{port}"
