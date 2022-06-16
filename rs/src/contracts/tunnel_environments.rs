// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use crate::contracts::tunnel_service_properties::*;
use url::Url;

pub struct TunnelEnvironment {
    pub service_uri: Url,
    pub service_app_id: String,
    pub service_internal_app_id: String,
    pub github_app_client_id: String,
}

pub fn env_production() -> TunnelEnvironment {
    TunnelEnvironment {
        service_uri: Url::parse(&format!("https://{}", PROD_DNS_NAME)).unwrap(),
        service_app_id: PROD_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: PROD_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}

pub fn env_staging() -> TunnelEnvironment {
    TunnelEnvironment {
        service_uri: Url::parse(&format!("https://{}", PPE_DNS_NAME)).unwrap(),
        service_app_id: PROD_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: PPE_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: NON_PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}

pub fn env_development() -> TunnelEnvironment {
    TunnelEnvironment {
        service_uri: Url::parse(&format!("https://{}", DEV_DNS_NAME)).unwrap(),
        service_app_id: NON_PROD_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: DEV_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: NON_PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}
