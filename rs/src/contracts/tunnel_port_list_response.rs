// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortListResponse.cs

use crate::contracts::TunnelPort;
use serde::{Deserialize, Serialize};

// Data contract for response of a list tunnel ports call.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelPortListResponse {
    // List of tunnels
    pub value: Vec<TunnelPort>,

    // Link to get next page of results
    pub next_link: Option<String>,
}
