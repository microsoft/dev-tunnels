// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ServiceVersionDetails.cs

use serde::{Deserialize, Serialize};

// Data contract for service version details.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct ServiceVersionDetails {
    // Gets or sets the version of the service. E.g. "1.0.6615.53976". The version
    // corresponds to the build number.
    pub version: Option<String>,

    // Gets or sets the commit ID of the service.
    pub commit_id: Option<String>,

    // Gets or sets the commit date of the service.
    pub commit_date: Option<String>,

    // Gets or sets the cluster ID of the service that handled the request.
    pub cluster_id: Option<String>,

    // Gets or sets the Azure location of the service that handled the request.
    pub azure_location: Option<String>,
}
