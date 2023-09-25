// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegion.cs

package tunnels

// Tunnel list by region.
type TunnelListByRegion struct {
	// Azure region name.
	RegionName string `json:"regionName,omitempty"`

	// Cluster id in the region.
	ClusterID  string `json:"clusterId,omitempty"`

	// List of tunnels.
	Value      []TunnelV2 `json:"value,omitempty"`

	// Error detail if getting list of tunnels in the region failed.
	Error      *ErrorDetail `json:"error,omitempty"`
}
