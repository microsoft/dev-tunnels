// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelServiceProperties.cs

use serde::{Deserialize, Serialize};

// Provides environment-dependent properties about the service.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelServiceProperties {
    // Gets the base URI of the service.
    pub service_uri: String,

    // Gets the public AAD AppId for the service.
    //
    // Clients specify this AppId as the audience property when authenticating to the
    // service.
    pub service_app_id: String,

    // Gets the internal AAD AppId for the service.
    //
    // Other internal services specify this AppId as the audience property when
    // authenticating to the tunnel service. Production services must be in the AME tenant
    // to use this appid.
    pub service_internal_app_id: String,

    // Gets the client ID for the service's GitHub app.
    //
    // Clients apps that authenticate tunnel users with GitHub specify this as the client
    // ID when requesting a user token.
    pub github_app_client_id: String,
}

// Global DNS name of the production tunnel service.
pub const PROD_DNS_NAME: &str = "global.rel.tunnels.api.visualstudio.com";

// Global DNS name of the pre-production tunnel service.
pub const PPE_DNS_NAME: &str = "global.rel.tunnels.ppe.api.visualstudio.com";

// Global DNS name of the development tunnel service.
pub const DEV_DNS_NAME: &str = "global.ci.tunnels.dev.api.visualstudio.com";

// First-party app ID: `Visual Studio Tunnel Service`
//
// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
// in the PROD service environment.
pub const PROD_FIRST_PARTY_APP_ID: &str = "46da2f7e-b5ef-422a-88d4-2a7f9de6a0b2";

// First-party app ID: `Visual Studio Tunnel Service - Test`
//
// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
// in the PPE service environments.
pub const PPE_FIRST_PARTY_APP_ID: &str = "54c45752-bacd-424a-b928-652f3eca2b18";

// First-party app ID: `DEV-VSTunnels`
//
// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
// in the DEV service environment
pub const DEV_FIRST_PARTY_APP_ID: &str = "9c63851a-ba2b-40a5-94bd-890be43b9284";

// Third-party app ID: `tunnels-prod-app-sp`
//
// Used for authenticating internal AAD service principals in the AME tenant, in the PROD
// service environment.
pub const PROD_THIRD_PARTY_APP_ID: &str = "ce65d243-a913-4cae-a7dd-cb52e9f77647";

// Third-party app ID: `tunnels-ppe-app-sp`
//
// Used for authenticating internal AAD service principals in the AME tenant, in the PPE
// service environment.
pub const PPE_THIRD_PARTY_APP_ID: &str = "544167a6-f431-4518-aac6-2fd50071928e";

// Third-party app ID: `tunnels-dev-app-sp`
//
// Used for authenticating internal AAD service principals in the corp tenant (not AME!),
// in the DEV service environment.
pub const DEV_THIRD_PARTY_APP_ID: &str = "a118c979-0249-44bb-8f95-eb0457127aeb";

// GitHub App Client ID for 'Visual Studio Tunnel Service'
//
// Used by client apps that authenticate tunnel users with GitHub, in the PROD service
// environment.
pub const PROD_GITHUB_APP_CLIENT_ID: &str = "Iv1.e7b89e013f801f03";

// GitHub App Client ID for 'Visual Studio Tunnel Service - Test'
//
// Used by client apps that authenticate tunnel users with GitHub, in the PPE and DEV
// service environments.
pub const NON_PROD_GITHUB_APP_CLIENT_ID: &str = "Iv1.b231c327f1eaa229";
