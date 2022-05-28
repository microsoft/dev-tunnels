// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ResourceStatus.cs

use serde::{Serialize, Deserialize};
use std::u64;

// Current value and limit for a limited resource related to a tunnel or tunnel port.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ResourceStatus {
    // Gets or sets the current value.
    current: u64,

    // Gets or sets the limit enforced by the service, or null if there is no limit.
    //
    // Any requests that would cause the limit to be exceeded may be denied by the
    // service. For HTTP requests, the response is generally a 403 Forbidden status, with
    // details about the limit in the response body.
    limit: Option<u64>,
}
