// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortListResponse.cs

package tunnels

// Data contract for response of a list tunnel ports call.
type TunnelPortListResponse struct {
	// List of tunnels
	Value    []TunnelPort `json:"value,omitempty"`

	// Link to get next page of results
	NextLink string `json:"nextLink,omitempty"`
}
