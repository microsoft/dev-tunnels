// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs

use crate::contracts::TunnelEndpoint;
use serde::{Deserialize, Serialize};

// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelRelayTunnelEndpoint {
    #[serde(flatten)]
    pub base: TunnelEndpoint,

    // Gets or sets the host URI.
    pub host_relay_uri: Option<String>,

    // Gets or sets the client URI.
    pub client_relay_uri: Option<String>,
}
