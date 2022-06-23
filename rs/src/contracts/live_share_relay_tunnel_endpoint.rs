// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/LiveShareRelayTunnelEndpoint.cs

use serde::{Deserialize, Serialize};

// Parameters for connecting to a tunnel via a Live Share Azure Relay.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct LiveShareRelayTunnelEndpoint {
    // Gets or sets the Live Share workspace ID.
    pub workspace_id: String,

    // Gets or sets the Azure Relay URI.
    pub relay_uri: Option<String>,

    // Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
    pub relay_host_sas_token: Option<String>,

    // Gets or sets a SAS token that allows clients to connect to the Azure Relay
    // endpoint.
    pub relay_client_sas_token: Option<String>,
}
