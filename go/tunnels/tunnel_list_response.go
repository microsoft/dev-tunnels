// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListResponse.cs

package tunnels

// Data contract for response of a list tunnel call.
type TunnelListResponse struct {
	// List of tunnels
	Value    []TunnelV2 `json:"value,omitempty"`

	// Link to get next page of results
	NextLink string `json:"nextLink,omitempty"`
}
