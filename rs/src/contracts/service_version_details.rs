// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ServiceVersionDetails.cs

use serde::{Serialize, Deserialize};

// Data contract for service version details.
#[derive(Serialize, Deserialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ServiceVersionDetails {
    // Gets or sets the version of the service. E.g. "1.0.6615.53976". The version
    // corresponds to the build number.
    version: Option<String>,

    // Gets or sets the commit ID of the service.
    commit_id: Option<String>,

    // Gets or sets the commit date of the service.
    commit_date: Option<String>,

    // Gets or sets the cluster ID of the service that handled the request.
    cluster_id: Option<String>,

    // Gets or sets the Azure location of the service that handled the request.
    azure_location: Option<String>,
}
