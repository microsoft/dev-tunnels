// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use crate::contracts::tunnel_service_properties::*;

pub fn env_production() -> TunnelServiceProperties {
    TunnelServiceProperties {
        service_uri: format!("https://{}", PROD_DNS_NAME),
        service_app_id: PROD_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: PROD_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}

pub fn env_staging() -> TunnelServiceProperties {
    TunnelServiceProperties {
        service_uri: format!("https://{}", PPE_DNS_NAME),
        service_app_id: PROD_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: PPE_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: NON_PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}

pub fn env_development() -> TunnelServiceProperties {
    TunnelServiceProperties {
        service_uri: format!("https://{}", DEV_DNS_NAME),
        service_app_id: DEV_FIRST_PARTY_APP_ID.to_owned(),
        service_internal_app_id: DEV_THIRD_PARTY_APP_ID.to_owned(),
        github_app_client_id: NON_PROD_GITHUB_APP_CLIENT_ID.to_owned(),
    }
}
