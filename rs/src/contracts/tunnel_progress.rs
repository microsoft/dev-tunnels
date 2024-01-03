// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

use serde::{Deserialize, Serialize};
use std::fmt;

// Specifies the tunnel progress events that are reported.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub enum TunnelProgress {
    // Starting refresh ports.
    StartingRefreshPorts,

    // Completed refresh ports.
    CompletedRefreshPorts,

    // Starting request uri for a tunnel service request.
    StartingRequestUri,

    // Starting request configuration for a tunnel service request.
    StartingRequestConfig,

    // Starting to send tunnel service request.
    StartingSendTunnelRequest,

    // Completed sending a tunnel service request.
    CompletedSendTunnelRequest,

    // Starting create tunnel port.
    StartingCreateTunnelPort,

    // Completed create tunnel port.
    CompletedCreateTunnelPort,

    // Starting get tunnel port.
    StartingGetTunnelPort,

    // Completed get tunnel port.
    CompletedGetTunnelPort,
}

impl fmt::Display for TunnelProgress {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            TunnelProgress::StartingRefreshPorts => write!(f, "StartingRefreshPorts"),
            TunnelProgress::CompletedRefreshPorts => write!(f, "CompletedRefreshPorts"),
            TunnelProgress::StartingRequestUri => write!(f, "StartingRequestUri"),
            TunnelProgress::StartingRequestConfig => write!(f, "StartingRequestConfig"),
            TunnelProgress::StartingSendTunnelRequest => write!(f, "StartingSendTunnelRequest"),
            TunnelProgress::CompletedSendTunnelRequest => write!(f, "CompletedSendTunnelRequest"),
            TunnelProgress::StartingCreateTunnelPort => write!(f, "StartingCreateTunnelPort"),
            TunnelProgress::CompletedCreateTunnelPort => write!(f, "CompletedCreateTunnelPort"),
            TunnelProgress::StartingGetTunnelPort => write!(f, "StartingGetTunnelPort"),
            TunnelProgress::CompletedGetTunnelPort => write!(f, "CompletedGetTunnelPort"),
        }
    }
}
