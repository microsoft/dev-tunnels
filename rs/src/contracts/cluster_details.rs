// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterDetails.cs

use serde::{Deserialize, Serialize};

// Tunnel service cluster details.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ClusterDetails {
    // A cluster identifier based on its region.
    pub cluster_id: Option<String>,

    // The cluster DNS host.
    pub host: Option<String>,
}
