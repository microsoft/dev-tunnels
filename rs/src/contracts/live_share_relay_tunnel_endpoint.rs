// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/LiveShareRelayTunnelEndpoint.cs

use crate::contracts::serialization::empty_string_as_none;
use serde::{Deserialize, Serialize};

// Parameters for connecting to a tunnel via a Live Share Azure Relay.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct LiveShareRelayTunnelEndpoint {
    // Gets or sets the Live Share workspace ID.
    pub workspace_id: String,

    // Gets or sets the Azure Relay URI.
    #[serde(default, deserialize_with = "empty_string_as_none")]
    pub relay_uri: Option<String>,

    // Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
    #[serde(default, deserialize_with = "empty_string_as_none")]
    pub relay_host_sas_token: Option<String>,

    // Gets or sets a SAS token that allows clients to connect to the Azure Relay
    // endpoint.
    #[serde(default, deserialize_with = "empty_string_as_none")]
    pub relay_client_sas_token: Option<String>,
}
