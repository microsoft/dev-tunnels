// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegionResponse.cs

use crate::contracts::TunnelListByRegion;
use serde::{Deserialize, Serialize};

// Data contract for response of a list tunnel by region call.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelListByRegionResponse {
    // List of tunnels
    #[serde(skip_serializing_if = "Vec::is_empty", default)]
    pub value: Vec<TunnelListByRegion>,

    // Link to get next page of results.
    pub next_link: Option<String>,
}
