// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortStatus.cs

use crate::contracts::RateStatus;
use crate::contracts::ResourceStatus;
use serde::{Deserialize, Serialize};

// Data contract for `TunnelPort` status.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelPortStatus {
    // Gets or sets the current value and limit for the number of clients connected to the
    // port.
    //
    // This client connection count does not include non-port-specific connections such as
    // SDK and SSH clients. See `TunnelStatus.ClientConnectionCount` for status of those
    // connections.  This count also does not include HTTP client connections, unless they
    // are upgraded to websockets. HTTP connections are counted per-request rather than
    // per-connection: see `TunnelPortStatus.HttpRequestRate`.
    pub client_connection_count: Option<ResourceStatus>,

    // Gets or sets the UTC date time when a client was last connected to the port, or
    // null if a client has never connected.
    pub last_client_connection_time: Option<String>,

    // Gets or sets the current value and limit for the rate of client connections to the
    // tunnel port.
    //
    // This client connection rate does not count non-port-specific connections such as
    // SDK and SSH clients. See `TunnelStatus.ClientConnectionRate` for those connection
    // types.  This also does not include HTTP connections, unless they are upgraded to
    // websockets. HTTP connections are counted per-request rather than per-connection:
    // see `TunnelPortStatus.HttpRequestRate`.
    pub client_connection_rate: Option<RateStatus>,

    // Gets or sets the current value and limit for the rate of HTTP requests to the
    // tunnel port.
    pub http_request_rate: Option<RateStatus>,
}
