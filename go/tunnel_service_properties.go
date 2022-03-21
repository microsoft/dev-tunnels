// Generated from ../../../cs/src/Contracts/TunnelServiceProperties.cs

package tunnels

// Provides environment-dependent properties about the service.
type TunnelServiceProperties struct {
	// Gets the base URI of the service.
	ServiceURI           string `json:"serviceUri"`

	// Gets the public AAD AppId for the service.
	//
	// Clients specify this AppId as the audience property when authenticating to the
	// service.
	ServiceAppID         string `json:"serviceAppId"`

	// Gets the internal AAD AppId for the service.
	//
	// Other internal services specify this AppId as the audience property when
	// authenticating to the tunnel service. Production services must be in the AME tenant to
	// use this appid.
	ServiceInternalAppID string `json:"serviceInternalAppId"`

	// Gets the client ID for the service's GitHub app.
	//
	// Clients apps that authenticate tunnel users with GitHub specify this as the client ID
	// when requesting a user token.
	GitHubAppClientID    string `json:"gitHubAppClientId"`
}
