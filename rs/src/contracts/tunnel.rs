// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/Tunnel.cs

use chrono::{DateTime, Utc};
use crate::contracts::TunnelAccessControl;
use crate::contracts::TunnelEndpoint;
use crate::contracts::TunnelOptions;
use crate::contracts::TunnelPort;
use crate::contracts::TunnelStatus;
use serde::{Serialize, Deserialize};
use std::collections::HashMap;

// Data contract for tunnel objects managed through the tunnel service REST API.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct Tunnel {
    // Gets or sets the ID of the cluster the tunnel was created in.
    cluster_id: Option<String>,

    // Gets or sets the generated ID of the tunnel, unique within the cluster.
    tunnel_id: Option<String>,

    // Gets or sets the optional short name (alias) of the tunnel.
    //
    // The name must be globally unique within the parent domain, and must be a valid
    // subdomain.
    name: Option<String>,

    // Gets or sets the description of the tunnel.
    description: Option<String>,

    // Gets or sets the tags of the tunnel.
    tags: Vec<String>,

    // Gets or sets the optional parent domain of the tunnel, if it is not using the
    // default parent domain.
    domain: Option<String>,

    // Gets or sets a dictionary mapping from scopes to tunnel access tokens.
    access_tokens: Option<HashMap<String, String>>,

    // Gets or sets access control settings for the tunnel.
    //
    // See `TunnelAccessControl` documentation for details about the access control model.
    access_control: Option<TunnelAccessControl>,

    // Gets or sets default options for the tunnel.
    options: Option<TunnelOptions>,

    // Gets or sets current connection status of the tunnel.
    status: Option<TunnelStatus>,

    // Gets or sets an array of endpoints where hosts are currently accepting client
    // connections to the tunnel.
    endpoints: Vec<TunnelEndpoint>,

    // Gets or sets a list of ports in the tunnel.
    //
    // This optional property enables getting info about all ports in a tunnel at the same
    // time as getting tunnel info, or creating one or more ports at the same time as
    // creating a tunnel. It is omitted when listing (multiple) tunnels, or when updating
    // tunnel properties. (For the latter, use APIs to create/update/delete individual
    // ports instead.)
    ports: Vec<TunnelPort>,

    // Gets or sets the time in UTC of tunnel creation.
    created: Option<DateTime<Utc>>,
}
