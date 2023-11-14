// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterDetails.cs

package tunnels

// Details of a tunneling service cluster. Each cluster represents an instance of the
// tunneling service running in a particular Azure region. New tunnels are created in the
// current region unless otherwise specified.
type ClusterDetails struct {
	// A cluster identifier based on its region.
	ClusterID     string `json:"clusterId"`

	// The URI of the service cluster.
	URI           string `json:"uri"`

	// The Azure location of the cluster.
	AzureLocation string `json:"azureLocation"`
}
