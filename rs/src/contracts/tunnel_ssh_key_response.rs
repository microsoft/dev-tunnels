// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelSshKeyResponse.cs

use serde::{Deserialize, Serialize};

// Response for SshKey endpoint.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelSshKeyResponse {
    // Gets or sets the ssh key for a tunnel.
    pub ssh_key: Option<String>,
}
