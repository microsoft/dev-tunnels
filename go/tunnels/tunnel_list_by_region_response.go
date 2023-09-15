// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegionResponse.cs

package tunnels

// Data contract for response of a list tunnel by region call.
type TunnelListByRegionResponse struct {
	// List of tunnels
	Value    []TunnelListByRegion `json:"value,omitempty"`

	// Link to get next page of results.
	NextLink string `json:"nextLink,omitempty"`
}
