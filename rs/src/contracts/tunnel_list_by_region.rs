// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegion.cs

use crate::contracts::ErrorDetail;
use crate::contracts::Tunnel;
use serde::{Deserialize, Serialize};

// Tunnel list by region.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelListByRegion {
    // Azure region name.
    pub region_name: Option<String>,

    // Cluster id in the region.
    pub cluster_id: Option<String>,

    // List of tunnels.
    #[serde(skip_serializing_if = "Vec::is_empty", default)]
    pub value: Vec<Tunnel>,

    // Error detail if getting list of tunnels in the region failed.
    pub error: Option<ErrorDetail>,
}
