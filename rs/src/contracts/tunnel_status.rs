// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelStatus.cs

use crate::contracts::RateStatus;
use crate::contracts::ResourceStatus;
use serde::{Deserialize, Serialize};

// Data contract for `Tunnel` status.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelStatus {
    // Gets or sets the current value and limit for the number of ports on the tunnel.
    pub port_count: Option<ResourceStatus>,

    // Gets or sets the current value and limit for the number of hosts currently
    // accepting connections to the tunnel.
    //
    // This is typically 0 or 1, but may be more than 1 if the tunnel options allow
    // multiple hosts.
    pub host_connection_count: Option<ResourceStatus>,

    // Gets or sets the UTC time when a host was last accepting connections to the tunnel,
    // or null if a host has never connected.
    pub last_host_connection_time: Option<String>,

    // Gets or sets the current value and limit for the number of clients connected to the
    // tunnel.
    //
    // This counts non-port-specific client connections, which is SDK and SSH clients. See
    // `TunnelPortStatus` for status of per-port client connections.
    pub client_connection_count: Option<ResourceStatus>,

    // Gets or sets the UTC time when a client last connected to the tunnel, or null if a
    // client has never connected.
    //
    // This reports times for non-port-specific client connections, which is SDK client
    // and SSH clients. See `TunnelPortStatus` for per-port client connections.
    pub last_client_connection_time: Option<String>,

    // Gets or sets the current value and limit for the rate of client connections to the
    // tunnel.
    //
    // This counts non-port-specific client connections, which is SDK client and SSH
    // clients. See `TunnelPortStatus` for status of per-port client connections.
    pub client_connection_rate: Option<RateStatus>,

    // Gets or sets the current value and limit for the rate of bytes being received by
    // the tunnel host and uploaded by tunnel clients.
    //
    // All types of tunnel and port connections, from potentially multiple clients, can
    // contribute to this rate. The reported rate may differ slightly from the rate
    // measurable by applications, due to protocol overhead. Data rate status reporting is
    // delayed by a few seconds, so this value is a snapshot of the data transfer rate
    // from a few seconds earlier.
    pub upload_rate: Option<RateStatus>,

    // Gets or sets the current value and limit for the rate of bytes being sent by the
    // tunnel host and downloaded by tunnel clients.
    //
    // All types of tunnel and port connections, from potentially multiple clients, can
    // contribute to this rate. The reported rate may differ slightly from the rate
    // measurable by applications, due to protocol overhead. Data rate status reporting is
    // delayed by a few seconds, so this value is a snapshot of the data transfer rate
    // from a few seconds earlier.
    pub download_rate: Option<RateStatus>,

    // Gets or sets the total number of bytes received by the tunnel host and uploaded by
    // tunnel clients, over the lifetime of the tunnel.
    //
    // All types of tunnel and port connections, from potentially multiple clients, can
    // contribute to this total. The reported value may differ slightly from the value
    // measurable by applications, due to protocol overhead. Data transfer status
    // reporting is delayed by a few seconds.
    pub upload_total: Option<u64>,

    // Gets or sets the total number of bytes sent by the tunnel host and downloaded by
    // tunnel clients, over the lifetime of the tunnel.
    //
    // All types of tunnel and port connections, from potentially multiple clients, can
    // contribute to this total. The reported value may differ slightly from the value
    // measurable by applications, due to protocol overhead. Data transfer status
    // reporting is delayed by a few seconds.
    pub download_total: Option<u64>,

    // Gets or sets the current value and limit for the rate of management API read
    // operations  for the tunnel or tunnel ports.
    pub api_read_rate: Option<RateStatus>,

    // Gets or sets the current value and limit for the rate of management API update
    // operations for the tunnel or tunnel ports.
    pub api_update_rate: Option<RateStatus>,
}
