// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntry.cs

package tunnels

// Constants for well-known identity providers.
type Providers []Provider
type Provider string

const (
	// Microsoft (AAD) identity provider.
	ProviderMicrosoft Provider = "microsoft"

	// GitHub identity provider.
	ProviderGitHub    Provider = "github"

	// SSH public keys.
	ProviderSsh       Provider = "ssh"

	// IPv4 addresses.
	ProviderIPv4      Provider = "ipv4"

	// IPv6 addresses.
	ProviderIPv6      Provider = "ipv6"
)
