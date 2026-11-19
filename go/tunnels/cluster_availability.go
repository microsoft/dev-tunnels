// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterAvailability.cs

package tunnels

// Availability status of a tunneling service cluster.
type ClusterAvailability string

const (
	// Cluster has sufficient capacity and is fully available.
	ClusterAvailabilityAvailable   ClusterAvailability = "Available"

	// Cluster is approaching capacity limits and may experience delays.
	ClusterAvailabilityDegraded    ClusterAvailability = "Degraded"

	// Cluster is at or beyond capacity and should not be used for new tunnels.
	ClusterAvailabilityUnavailable ClusterAvailability = "Unavailable"
)
