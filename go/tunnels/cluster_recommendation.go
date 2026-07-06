// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendation.cs

package tunnels

// A single cluster recommendation with availability and capacity details.
type ClusterRecommendation struct {
	// Gets or sets the cluster ID, e.g. "usw2".
	ClusterID          string `json:"clusterId"`

	// Gets or sets the Azure location name, e.g. "WestUs2".
	AzureLocation      string `json:"azureLocation"`

	// Gets or sets the Azure geography name for data residency, e.g. "United States".
	AzureGeo           string `json:"azureGeo"`

	// Gets or sets the cluster URI for API requests.
	ClusterURI         string `json:"clusterUri"`

	// Gets or sets the availability status of the cluster.
	Availability       ClusterAvailability `json:"availability"`

	// Gets or sets the utilization percentage of the cluster.
	UtilizationPercent float64 `json:"utilizationPercent"`

	// Gets or sets a human-readable reason for this recommendation's ranking.
	Reason             string `json:"reason"`
}
