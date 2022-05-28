// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/LiveShareRelayTunnelEndpoint.cs

use serde::{Serialize, Deserialize};

// Parameters for connecting to a tunnel via a Live Share Azure Relay.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct LiveShareRelayTunnelEndpoint {
    // Gets or sets the Live Share workspace ID.
    workspace_id: String,

    // Gets or sets the Azure Relay URI.
    relay_uri: Option<String>,

    // Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
    relay_host_sas_token: Option<String>,

    // Gets or sets a SAS token that allows clients to connect to the Azure Relay
    // endpoint.
    relay_client_sas_token: Option<String>,
}
