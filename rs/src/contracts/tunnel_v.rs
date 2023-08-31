// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelV1.cs

use crate::contracts::TunnelBase;
use serde::{Deserialize, Serialize};

// Tunnel type used for tunnel service API version 2023-05-23-preview
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelV {
    #[serde(flatten)]
    pub base: TunnelBase,

    // Gets or sets the ID of the tunnel, unique within the cluster.
    pub tunnel_id: Option<String>,

    // Gets or sets the tags of the tunnel.
    #[serde(skip_serializing_if = "Vec::is_empty", default)]
    pub tags: Vec<String>,
}
