// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntry.cs

package tunnels

// Data contract for an access control entry on a `Tunnel` or `TunnelPort`.
//
// An access control entry (ACE) grants or denies one or more access scopes to one or more
// subjects. Tunnel ports inherit access control entries from their tunnel, and they may
// have additional port-specific entries that augment or override those access rules.
type TunnelAccessControlEntry struct {
	// Gets or sets the access control entry type.
	Type         TunnelAccessControlEntryType `json:"type"`

	// Gets or sets the provider of the subjects in this access control entry. The provider
	// impacts how the subject identifiers are resolved and displayed. The provider may be an
	// identity provider such as AAD, or a system or standard such as "ssh" or "ipv4".
	//
	// For user, group, or org ACEs, this value is the name of the identity provider of the
	// user/group/org IDs. It may be one of the well-known provider names in
	// `TunnelAccessControlEntry.Providers`, or (in the future) a custom identity provider. 
	// For public key ACEs, this value is the type of public key, e.g. "ssh".  For IP address
	// range ACEs, this value is the IP addrss version, e.g. "ipv4" or "ipv6".  For anonymous
	// ACEs, this value is null.
	Provider     string `json:"provider"`

	// Gets or sets a value indicating whether this is an access control entry on a tunnel
	// port that is inherited from the tunnel's access control list.
	IsInherited  bool `json:"isInherited,omitempty"`

	// Gets or sets a value indicating whether this entry is a deny rule that blocks access
	// to the specified users. Otherwise it is an allow role.
	//
	// All deny rules (including inherited rules) are processed after all allow rules.
	// Therefore a deny rule cannot be overridden by an allow rule that is later in the list
	// or on a more-specific resource.
	IsDeny       bool `json:"isDeny,omitempty"`

	// Gets or sets an optional organization context for all subjects of this entry. The use
	// and meaning of this value depends on the `TunnelAccessControlEntry.Type` and
	// `TunnelAccessControlEntry.Provider` of this entry.
	//
	// For AAD users and group ACEs, this value is the AAD tenant ID. It is not currently
	// used with any other types of ACEs.
	Organization string `json:"organization"`

	// Gets or sets the subjects for the entry, such as user or group IDs. The format of the
	// values depends on the `TunnelAccessControlEntry.Type` and
	// `TunnelAccessControlEntry.Provider` of this entry.
	Subjects     []string `json:"subjects"`

	// Gets or sets the access scopes that this entry grants or denies to the subjects.
	//
	// These must be one or more values from `TunnelAccessScopes`.
	Scopes       []string `json:"scopes"`
}

// Constants for well-known identity providers.
type TunnelAccessControlEntryProviders []TunnelAccessControlEntryProvider
type TunnelAccessControlEntryProvider string

const (
	// Microsoft (AAD) identity provider.
	TunnelAccessControlEntryProviderMicrosoft TunnelAccessControlEntryProvider = "microsoft"

	// GitHub identity provider.
	TunnelAccessControlEntryProviderGitHub    TunnelAccessControlEntryProvider = "github"

	// SSH public keys.
	TunnelAccessControlEntryProviderSsh       TunnelAccessControlEntryProvider = "ssh"

	// IPv4 addresses.
	TunnelAccessControlEntryProviderIPv4      TunnelAccessControlEntryProvider = "ipv4"

	// IPv6 addresses.
	TunnelAccessControlEntryProviderIPv6      TunnelAccessControlEntryProvider = "ipv6"
)
