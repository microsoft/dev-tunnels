// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

use serde::{Deserialize, Serialize};

// Event args for the tunnel report progress event.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelReportProgressEventArgs {
    // Specifies the progress event that is being reported. See `TunnelProgress` and
    // Ssh.Progress for a description of the different progress events that can be
    // reported.
    pub progress: String,

    // The session number associated with an SSH session progress event.
    pub session_number: Option<i32>,
}
