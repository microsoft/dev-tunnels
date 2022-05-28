// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ProblemDetails.cs

use serde::{Serialize, Deserialize};
use std::collections::HashMap;

// Structure of error details returned by the tunnel service, including validation errors.
//
// This object may be returned with a response status code of 400 (or other 4xx code). It
// is compatible with RFC 7807 Problem Details (https://tools.ietf.org/html/rfc7807) and
// https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails but
// doesn't require adding a dependency on that package.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ProblemDetails {
    // Gets or sets the error title.
    title: Option<String>,

    // Gets or sets the error detail.
    detail: Option<String>,

    // Gets or sets additional details about individual request properties.
    errors: Option<HashMap<String, Vec<String>>>,
}
