// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendationResponse.cs

use crate::contracts::ClusterRecommendation;
use serde::{Deserialize, Serialize};

// Response from the cluster recommendation API containing ranked cluster recommendations.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ClusterRecommendationResponse {
    // Gets or sets the preferred cluster ID that was requested, if any.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub preferred_cluster_id: Option<String>,

    // Gets or sets the recommended cluster ID — the best available cluster. Null if no
    // clusters are available.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub recommended_cluster_id: Option<String>,

    // Gets or sets a value indicating whether the recommendation differs from the
    // preferred cluster.
    pub is_fallback: bool,

    // Gets or sets the ordered list of cluster recommendations, ranked by preference.
    pub recommendations: Vec<ClusterRecommendation>,
}
