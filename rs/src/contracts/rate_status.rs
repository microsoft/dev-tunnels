// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/RateStatus.cs

use crate::contracts::NamedRateStatus;
use serde::{Deserialize, Serialize};

// Current value and limit information for a rate-limited operation related to a tunnel or
// port.
#[derive(Clone, Debug, Deserialize, Serialize, Default)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct RateStatus {
    // Gets or sets the length of each period, in seconds, over which the rate is
    // measured.
    //
    // For rates that are limited by month (or billing period), this value may represent
    // an estimate, since the actual duration may vary by the calendar.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub period_seconds: Option<u32>,

    // Gets or sets the unix time in seconds when this status will be reset.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reset_time: Option<i64>,

    #[serde(flatten)]
    pub named_rate_status: NamedRateStatus,
}
