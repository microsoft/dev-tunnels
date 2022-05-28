// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelOptions.cs

use serde::{Serialize, Deserialize};

// Data contract for `Tunnel` or `TunnelPort` options.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelOptions {
    // Gets or sets a value indicating whether web-forwarding of this tunnel can run on
    // any cluster (region) without redirecting to the home cluster. This is only
    // applicable if the tunnel has a name and web-forwarding uses it.
    is_globally_available: bool,
}
