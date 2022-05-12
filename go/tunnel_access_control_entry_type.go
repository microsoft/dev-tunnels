// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntryType.cs

package tunnels

// Specifies the type of `TunnelAccessControlEntry`.
type TunnelAccessControlEntryType string

const (
	// Uninitialized access control entry type.
	TunnelAccessControlEntryTypeNone TunnelAccessControlEntryType = "none"

	// The access control entry refers to all anonymous users.
	TunnelAccessControlEntryTypeAnonymous TunnelAccessControlEntryType = "anonymous"

	// The access control entry is a list of user IDs that are allowed (or denied) access.
	TunnelAccessControlEntryTypeUsers TunnelAccessControlEntryType = "users"

	// The access control entry is a list of groups IDs that are allowed (or denied) access.
	TunnelAccessControlEntryTypeGroups TunnelAccessControlEntryType = "groups"

	// The access control entry is a list of organization IDs that are allowed (or denied)
	// access.
	//
	// All users in the organizations are allowed (or denied) access, unless overridden by
	// following group or user rules.
	TunnelAccessControlEntryTypeOrganizations TunnelAccessControlEntryType = "organizations"

	// The access control entry is a list of repositories. Users are allowed access to the
	// tunnel if they have access to the repo.
	TunnelAccessControlEntryTypeRepositories TunnelAccessControlEntryType = "repositories"

	// The access control entry is a list of public keys. Users are allowed access if they
	// can authenticate using a private key corresponding to one of the public keys.
	TunnelAccessControlEntryTypePublicKeys TunnelAccessControlEntryType = "publicKeys"

	// The access control entry is a list of IP address ranges that are allowed (or denied)
	// access to the tunnel.
	TunnelAccessControlEntryTypeIPAddressRanges TunnelAccessControlEntryType = "iPAddressRanges"
)
