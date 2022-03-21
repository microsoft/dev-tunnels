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
	HostID         string `json:"hostId"`

	// Gets or sets a string used to format URIs where a web client can connect to ports of
	// the tunnel. The string includes a `TunnelEndpoint.PortUriToken` that must be replaced
	// with the actual port number.
	PortURIFormat  string `json:"portUriFormat"`
}
