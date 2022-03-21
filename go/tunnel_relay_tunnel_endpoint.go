// Generated from ../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs

package tunnels

// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
type TunnelRelayTunnelEndpoint struct {
	TunnelEndpoint

	// Gets or sets the host URI.
	HostRelayURI   string `json:"hostRelayUri"`

	// Gets or sets the client URI.
	ClientRelayURI string `json:"clientRelayUri"`

	// Gets or sets an array of public keys, which can be used by clients to authenticate the
	// host.
	HostPublicKeys []string `json:"hostPublicKeys"`
}
