// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs

use crate::contracts::serialization::empty_string_as_none;
use serde::{Deserialize, Serialize};

// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelRelayTunnelEndpoint {
    // Gets or sets the host URI.
    #[serde(default, deserialize_with = "empty_string_as_none")]
    pub host_relay_uri: Option<String>,

    // Gets or sets the client URI.
    #[serde(default, deserialize_with = "empty_string_as_none")]
    pub client_relay_uri: Option<String>,
}
