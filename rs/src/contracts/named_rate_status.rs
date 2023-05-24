// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/NamedRateStatus.cs

use crate::contracts::RateStatus;
use serde::{Deserialize, Serialize};

// A named `RateStatus`.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct NamedRateStatus {
    #[serde(flatten)]
    pub base: RateStatus,

    // The name of the rate status.
    pub name: Option<String>,
}
