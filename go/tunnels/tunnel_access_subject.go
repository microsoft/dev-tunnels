// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessSubject.cs

package tunnels

// Properties about a subject of a tunnel access control entry (ACE), used when resolving
// subject names to IDs when creating new ACEs, or formatting subject IDs to names when
// displaying existing ACEs.
type TunnelAccessSubject struct {
	// Gets or sets the type of subject, e.g. user, group, or organization.
	Type           TunnelAccessControlEntryType `json:"type"`

	// Gets or sets the subject ID.
	//
	// The ID is typically a guid or integer that is unique within the scope of the identity
	// provider or organization, and never changes for that subject.
	ID             string `json:"id,omitempty"`

	// Gets or sets the subject organization ID, which may be required if an organization is
	// not implied by the authentication context.
	OrganizationID string `json:"organizationId,omitempty"`

	// Gets or sets the partial or full subject name.
	//
	// When resolving a subject name to ID, a partial name may be provided, and the full name
	// is returned if the partial name was successfully resolved. When formatting a subject
	// ID to name, the full name is returned if the ID was found.
	Name           string `json:"name,omitempty"`

	// Gets or sets an array of possible subject matches, if a partial name was provided and
	// did not resolve to a single subject.
	//
	// This property applies only when resolving subject names to IDs.
	Matches        []TunnelAccessSubject `json:"matches,omitempty"`
}
