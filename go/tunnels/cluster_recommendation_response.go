// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendationResponse.cs

package tunnels

// Response from the cluster recommendation API containing ranked cluster recommendations.
type ClusterRecommendationResponse struct {
	// Gets or sets the preferred cluster ID that was requested, if any.
	PreferredClusterID   string `json:"preferredClusterId"`

	// Gets or sets the recommended cluster ID — the best available cluster. Null if no
	// clusters are available.
	RecommendedClusterID string `json:"recommendedClusterId"`

	// Gets or sets a value indicating whether the recommendation differs from the preferred
	// cluster.
	IsFallback           bool `json:"isFallback"`

	// Gets or sets the ordered list of cluster recommendations, ranked by preference.
	Recommendations      []ClusterRecommendation `json:"recommendations"`
}
