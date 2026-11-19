// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelEvent.cs

use jiff::Timestamp;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

// Data contract for tunnel client events reported to the tunnel service.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelEvent {
    // Gets or sets the UTC timestamp of the event (using the client's clock).
    #[serde(skip_serializing_if = "Option::is_none")]
    pub timestamp: Option<Timestamp>,

    // Gets or sets name of the event. This should be a short descriptive identifier.
    pub name: String,

    // Gets or sets the severity of the event, such as `TunnelEvent.Info`,
    // `TunnelEvent.Warning`, or `TunnelEvent.Error`.
    //
    // If not specified, the default severity is "info".
    #[serde(skip_serializing_if = "Option::is_none")]
    pub severity: Option<String>,

    // Gets or sets optional unstructured details about the event, such as a message or
    // description. For warning or error events this may include a stack trace.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub details: Option<String>,

    // Gets or sets semi-structured event properties.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub properties: Option<HashMap<String, String>>,
}

// Default event severity.
pub const INFO: &str = "info";

// Warning event severity.
pub const WARNING: &str = "warning";

// Error event severity.
pub const ERROR: &str = "error";
