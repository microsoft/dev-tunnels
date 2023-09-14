// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListResponse.cs

use crate::contracts::TunnelV2;
use serde::{Deserialize, Serialize};

// Data contract for response of a list tunnel call.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelListResponse {
    // List of tunnels
    pub value: Vec<TunnelV2>,

    // Link to get next page of results
    pub next_link: Option<String>,
}
