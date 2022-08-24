// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterDetails.cs

package tunnels

// Tunnel service cluster details.
type ClusterDetails struct {
	// A cluster identifier based on its region.
	ClusterID string `json:"clusterId"`

	// The cluster DNS host.
	Host      string `json:"host"`
}
