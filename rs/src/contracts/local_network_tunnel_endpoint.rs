// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/LocalNetworkTunnelEndpoint.cs

use crate::contracts::TunnelEndpoint;
use serde::{Deserialize, Serialize};

// Parameters for connecting to a tunnel via a local network connection.
//
// While a direct connection is technically not "tunneling", tunnel hosts may accept
// connections via the local network as an optional more-efficient alternative to a relay.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct LocalNetworkTunnelEndpoint {
    #[serde(flatten)]
    pub base: TunnelEndpoint,

    // Gets or sets a list of IP endpoints where the host may accept connections.
    //
    // A host may accept connections on multiple IP endpoints simultaneously if there are
    // multiple network interfaces on the host system and/or if the host supports both
    // IPv4 and IPv6.  Each item in the list is a URI consisting of a scheme (which gives
    // an indication of the network connection protocol), an IP address (IPv4 or IPv6) and
    // a port number. The URIs do not typically include any paths, because the connection
    // is not normally HTTP-based.
    pub host_endpoints: Vec<String>,
}
