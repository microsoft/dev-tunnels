// Generated from ../../../cs/src/Contracts/LiveShareRelayTunnelEndpoint.cs

package tunnels

// Parameters for connecting to a tunnel via a Live Share Azure Relay.
type LiveShareRelayTunnelEndpoint struct {
	TunnelEndpoint

	// Gets or sets the Live Share workspace ID.
	WorkspaceID         string `json:"workspaceId"`

	// Gets or sets the Azure Relay URI.
	RelayURI            string `json:"relayUri"`

	// Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
	RelayHostSasToken   string `json:"relayHostSasToken"`

	// Gets or sets a SAS token that allows clients to connect to the Azure Relay endpoint.
	RelayClientSasToken string `json:"relayClientSasToken"`

	// Gets or sets an array of public keys, which can be used by clients to authenticate the
	// host.
	HostPublicKeys      []string `json:"hostPublicKeys"`
}
