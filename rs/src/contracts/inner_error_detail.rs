// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/InnerErrorDetail.cs

use serde::{Deserialize, Serialize};

// An object containing more specific information than the current object about the error.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct InnerErrorDetail {
    // A more specific error code than was provided by the containing error. One of a
    // server-defined set of error codes in `ErrorCodes`.
    pub code: String,

    // An object containing more specific information than the current object about the
    // error.
    #[serde(rename = "innererror")]
    pub inner_error: Option<Box<InnerErrorDetail>>,
}
