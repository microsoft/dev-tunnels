// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/RateStatus.cs

use crate::contracts::ResourceStatus;
use serde::{Deserialize, Serialize};

// Current value and limit information for a rate-limited operation related to a tunnel or
// port.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct RateStatus {
    #[serde(flatten)]
    pub base: ResourceStatus,

    // Gets or sets the length of each period, in seconds, over which the rate is
    // measured.
    //
    // For rates that are limited by month (or billing period), this value may represent
    // an estimate, since the actual duration may vary by the calendar.
    pub period_seconds: Option<u32>,

    // Gets or sets the unix time in seconds when this status will be reset.
    pub reset_time: Option<i64>,
}
