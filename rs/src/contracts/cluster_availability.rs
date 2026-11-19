// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterAvailability.cs

use serde::{Deserialize, Serialize};
use std::fmt;

// Availability status of a tunneling service cluster.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub enum ClusterAvailability {
    // Cluster has sufficient capacity and is fully available.
    Available,

    // Cluster is approaching capacity limits and may experience delays.
    Degraded,

    // Cluster is at or beyond capacity and should not be used for new tunnels.
    Unavailable,
}

impl fmt::Display for ClusterAvailability {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            ClusterAvailability::Available => write!(f, "Available"),
            ClusterAvailability::Degraded => write!(f, "Degraded"),
            ClusterAvailability::Unavailable => write!(f, "Unavailable"),
        }
    }
}
