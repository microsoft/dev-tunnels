// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/NamedRateStatus.cs

use serde::{Deserialize, Serialize};

// A named `RateStatus`.
#[derive(Clone, Debug, Deserialize, Serialize, Default)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct NamedRateStatus {
    // The name of the rate status.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
}
