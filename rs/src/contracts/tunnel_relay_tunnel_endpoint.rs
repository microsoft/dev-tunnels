// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelRelayTunnelEndpoint.cs

use serde::{Serialize, Deserialize};

// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelRelayTunnelEndpoint {
    // Gets or sets the host URI.
    host_relay_uri: Option<String>,

    // Gets or sets the client URI.
    client_relay_uri: Option<String>,
}
