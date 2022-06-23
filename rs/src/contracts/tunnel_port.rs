// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPort.cs

use crate::contracts::TunnelAccessControl;
use crate::contracts::TunnelOptions;
use crate::contracts::TunnelPortStatus;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

// Data contract for tunnel port objects managed through the tunnel service REST API.
#[derive(Clone, Debug, Deserialize, Serialize, Default)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelPort {
    // Gets or sets the ID of the cluster the tunnel was created in.
    pub cluster_id: Option<String>,

    // Gets or sets the generated ID of the tunnel, unique within the cluster.
    pub tunnel_id: Option<String>,

    // Gets or sets the IP port number of the tunnel port.
    pub port_number: u16,

    // Gets or sets the protocol of the tunnel port.
    //
    // Should be one of the string constants from `TunnelProtocol`.
    pub protocol: Option<String>,

    // Gets or sets a dictionary mapping from scopes to tunnel access tokens.
    //
    // Unlike the tokens in `Tunnel.AccessTokens`, these tokens are restricted to the
    // individual port.
    pub access_tokens: Option<HashMap<String, String>>,

    // Gets or sets access control settings for the tunnel port.
    //
    // See `TunnelAccessControl` documentation for details about the access control model.
    pub access_control: Option<TunnelAccessControl>,

    // Gets or sets options for the tunnel port.
    pub options: Option<TunnelOptions>,

    // Gets or sets current connection status of the tunnel port.
    pub status: Option<TunnelPortStatus>,
}
