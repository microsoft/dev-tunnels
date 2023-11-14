// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConnectionMode.cs

use serde::{Deserialize, Serialize};
use std::fmt;

// Specifies the connection protocol / implementation for a tunnel.
//
// Depending on the connection mode, hosts or clients might need to use different
// authentication and connection protocols.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub enum TunnelConnectionMode {
    // Connect directly to the host over the local network.
    //
    // While it's technically not "tunneling", this mode may be combined with others to
    // enable choosing the most efficient connection mode available.
    LocalNetwork,

    // Use the tunnel service's integrated relay function.
    TunnelRelay,
}

impl fmt::Display for TunnelConnectionMode {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            TunnelConnectionMode::LocalNetwork => write!(f, "LocalNetwork"),
            TunnelConnectionMode::TunnelRelay => write!(f, "TunnelRelay"),
        }
    }
}
