// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ErrorDetail.cs

use crate::contracts::InnerErrorDetail;
use serde::{Deserialize, Serialize};

// The top-level error object whose code matches the x-ms-error-code response header
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ErrorDetail {
    // One of a server-defined set of error codes defined in `ErrorCodes`.
    pub code: String,

    // A human-readable representation of the error.
    pub message: String,

    // The target of the error.
    pub target: Option<String>,

    // An array of details about specific errors that led to this reported error.
    #[serde(skip_serializing_if = "Vec::is_empty", default)]
    pub details: Vec<ErrorDetail>,

    // An object containing more specific information than the current object about the
    // error.
    #[serde(rename = "innererror")]
    pub inner_error: Option<InnerErrorDetail>,
}
