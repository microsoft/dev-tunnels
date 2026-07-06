// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendation.cs

use crate::contracts::ClusterAvailability;
use serde::{Deserialize, Serialize};

// A single cluster recommendation with availability and capacity details.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ClusterRecommendation {
    // Gets or sets the cluster ID, e.g. "usw2".
    pub cluster_id: String,

    // Gets or sets the Azure location name, e.g. "WestUs2".
    pub azure_location: String,

    // Gets or sets the Azure geography name for data residency, e.g. "United States".
    pub azure_geo: String,

    // Gets or sets the cluster URI for API requests.
    pub cluster_uri: String,

    // Gets or sets the availability status of the cluster.
    pub availability: ClusterAvailability,

    // Gets or sets the utilization percentage of the cluster.
    pub utilization_percent: f64,

    // Gets or sets a human-readable reason for this recommendation's ranking.
    pub reason: String,
}
