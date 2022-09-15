// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterDetails.cs

use serde::{Deserialize, Serialize};

// Details of a tunneling service cluster. Each cluster represents an instance of the
// tunneling service running in a particular Azure region. New tunnels are created in the
// current region unless otherwise specified.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ClusterDetails {
    // A cluster identifier based on its region.
    pub cluster_id: String,

    // The URI of the service cluster.
    pub uri: String,

    // The Azure location of the cluster.
    pub azure_location: String,
}
