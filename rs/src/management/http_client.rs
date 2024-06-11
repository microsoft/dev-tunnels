// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::sync::Arc;

use reqwest::{
    header::{HeaderValue, AUTHORIZATION, CONTENT_TYPE},
    Client, Method, Request,
};

use serde::{de::DeserializeOwned, Serialize};
use url::Url;

use rand::Rng;

use crate::contracts::{
    env_production, NamedRateStatus, Tunnel, TunnelEndpoint, TunnelListByRegionResponse,
    TunnelPort, TunnelPortListResponse, TunnelRelayTunnelEndpoint, TunnelServiceProperties,
};

use super::{
    Authorization, AuthorizationProvider, HttpError, HttpResult, ResponseError, TunnelLocator,
    TunnelRequestOptions, NO_REQUEST_OPTIONS,
};

use crate::management::policy_provider::get_policy_header_value;

#[derive(Clone)]
pub struct TunnelManagementClient {
    client: Client,
    authorization: Arc<Box<dyn AuthorizationProvider>>,
    pub(crate) user_agent: HeaderValue,
    environment: TunnelServiceProperties,
    api_version: String,
}

const TUNNELS_API_PATH: &str = "/tunnels";
const USER_LIMITS_API_PATH: &str = "/userlimits";
const ENDPOINTS_API_SUB_PATH: &str = "endpoints";
const PORTS_API_SUB_PATH: &str = "ports";
const CHECK_TUNNEL_NAME_SUB_PATH: &str = ":checkNameAvailability";
const PKG_VERSION: Option<&str> = option_env!("CARGO_PKG_VERSION");
const API_VERSIONS: &[&str] = &["2023-09-27-preview"];

impl TunnelManagementClient {
    /// Returns a builder that creates a new client, starting with the current
    /// client's options.
    pub fn build(&self) -> TunnelClientBuilder {
        TunnelClientBuilder {
            authorization: self.authorization.clone(),
            client: Some(self.client.clone()),
            user_agent: self.user_agent.clone(),
            environment: self.environment.clone(),
            api_version: self.api_version.clone(),
        }
    }

    /// Lists tunnels owned by the user.
    pub async fn list_all_tunnels(
        &self,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Vec<Tunnel>> {
        let mut url = self.build_uri(None, TUNNELS_API_PATH);
        url.query_pairs_mut().append_pair("global", "true");

        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        let response: TunnelListByRegionResponse =
            self.execute_json("list_all_tunnels", request).await?;
        Ok(response.value.into_iter().flat_map(|v| v.value).collect())
    }

    /// Lists tunnels owned by the user in a specific cluster.
    pub async fn list_cluster_tunnels(
        &self,
        cluster_id: &str,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Vec<Tunnel>> {
        let url = self.build_uri(Some(cluster_id), TUNNELS_API_PATH);
        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        let response: TunnelListByRegionResponse =
            self.execute_json("list_cluster_tunnels", request).await?;
        Ok(response.value.into_iter().flat_map(|v| v.value).collect())
    }

    /// Looks up a tunnel by ID or name.
    pub async fn get_tunnel(
        &self,
        locator: &TunnelLocator,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Tunnel> {
        let url = self.build_tunnel_uri(locator, None);
        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        self.execute_json("get_tunnel", request).await
    }

    /// Creates a new tunnel.
    pub async fn create_tunnel(
        &self,
        mut tunnel: Tunnel,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Tunnel> {
        let tunnel_id = tunnel
            .tunnel_id
            .take()
            .unwrap_or_else(TunnelManagementClient::generate_tunnel_id);

        let mut url = self.build_uri(tunnel.cluster_id.as_deref(), TUNNELS_API_PATH);
        let new_path = url.path().to_owned() + "/" + &tunnel_id;
        url.set_path(&new_path);
        tunnel.tunnel_id = Some(tunnel_id);

        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, tunnel);
        self.execute_json("create_tunnel", request).await
    }

    /// Gets if tunnel name is avilable.
    pub async fn check_name_availability(&self, name: &str) -> HttpResult<bool> {
        let path = format!(
            "{}/{}{}",
            TUNNELS_API_PATH, name, CHECK_TUNNEL_NAME_SUB_PATH
        );
        let url = self.build_uri(None, &path);

        let request = self
            .make_tunnel_request(Method::GET, url, NO_REQUEST_OPTIONS)
            .await?;
        self.execute_json("get_name_availability", request).await
    }

    /// Updates an existing tunnel.
    pub async fn update_tunnel(
        &self,
        tunnel: &Tunnel,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Tunnel> {
        let url = self.build_tunnel_uri(&tunnel.try_into().unwrap(), None);
        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, tunnel);
        self.execute_json("update_tunnel", request).await
    }

    /// Deletes an existing tunnel.
    pub async fn delete_tunnel(
        &self,
        locator: &TunnelLocator,
        options: &TunnelRequestOptions,
    ) -> HttpResult<bool> {
        let url = self.build_tunnel_uri(locator, None);
        let request = self
            .make_tunnel_request(Method::DELETE, url, options)
            .await?;
        self.execute_no_response("delete_tunnel", request).await
    }

    /// Updates an existing tunnel's endpoints.
    pub async fn update_tunnel_endpoints(
        &self,
        locator: &TunnelLocator,
        endpoint: &TunnelEndpoint,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelEndpoint> {
        let mut url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", ENDPOINTS_API_SUB_PATH, endpoint.id.as_deref().unwrap())),
        );
        url.query_pairs_mut()
            .append_pair("connectionMode", &endpoint.connection_mode.to_string());
        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, endpoint);
        self.execute_json("update_tunnel_endpoints", request).await
    }

    /// Updates an existing tunnel's endpoints with relay information.
    pub async fn update_tunnel_relay_endpoints(
        &self,
        locator: &TunnelLocator,
        endpoint: &TunnelRelayTunnelEndpoint,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelRelayTunnelEndpoint> {
        let mut url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", ENDPOINTS_API_SUB_PATH, endpoint.base.id.as_deref().unwrap())),
        );
        url.query_pairs_mut()
            .append_pair("connectionMode", &endpoint.base.connection_mode.to_string());
        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, endpoint);
        self.execute_json("update_tunnel_relay_endpoints", request)
            .await
    }

    /// Deletes an existing tunnel's endpoints.
    pub async fn delete_tunnel_endpoints(
        &self,
        locator: &TunnelLocator,
        id: &str,
        options: &TunnelRequestOptions,
    ) -> HttpResult<bool> {
        let path = format!("{}/{}", ENDPOINTS_API_SUB_PATH, id);

        let url = self.build_tunnel_uri(locator, Some(&path));
        let request = self
            .make_tunnel_request(Method::DELETE, url, options)
            .await?;
        self.execute_no_response("delete_tunnel_endpoints", request)
            .await
    }

    /// List a tunnel's ports.
    pub async fn list_tunnel_ports(
        &self,
        locator: &TunnelLocator,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Vec<TunnelPort>> {
        let url = self.build_tunnel_uri(locator, Some(PORTS_API_SUB_PATH));
        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        self.execute_json("list_tunnel_ports", request)
            .await
            .map(|r: TunnelPortListResponse| r.value)
    }

    /// Gets info about a specific tunnel port.
    pub async fn get_tunnel_port(
        &self,
        locator: &TunnelLocator,
        port_number: u16,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelPort> {
        let url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", PORTS_API_SUB_PATH, port_number)),
        );
        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        self.execute_json("get_tunnel_port", request).await
    }

    /// Creates a new port for a tunnel.
    pub async fn create_tunnel_port(
        &self,
        locator: &TunnelLocator,
        port: &TunnelPort,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelPort> {
        let url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", PORTS_API_SUB_PATH, port.port_number)),
        );
        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, port);
        self.execute_json("create_tunnel_port", request).await
    }

    /// Updates an existing port on the tunnel.
    pub async fn update_tunnel_port(
        &self,
        locator: &TunnelLocator,
        port: &TunnelPort,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelPort> {
        let url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", PORTS_API_SUB_PATH, port.port_number)),
        );
        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        json_body(&mut request, port);
        self.execute_json("create_tunnel_port", request).await
    }

    /// Deletes an existing port on the tunnel.
    pub async fn delete_tunnel_port(
        &self,
        locator: &TunnelLocator,
        port_number: u16,
        options: &TunnelRequestOptions,
    ) -> HttpResult<bool> {
        let url = self.build_tunnel_uri(
            locator,
            Some(&format!("{}/{}", PORTS_API_SUB_PATH, port_number)),
        );
        let request = self
            .make_tunnel_request(Method::DELETE, url, options)
            .await?;
        self.execute_no_response("delete_tunnel_port", request)
            .await
    }

    /// Lists all user limits.
    pub async fn list_user_limits(
        &self,
        options: &TunnelRequestOptions,
    ) -> HttpResult<Vec<NamedRateStatus>> {
        let url = self.build_uri(None, USER_LIMITS_API_PATH);

        let request = self.make_tunnel_request(Method::GET, url, options).await?;
        self.execute_json("list_user_limits", request).await
    }

    /// Sends the request and deserializes a JSON response
    #[cfg(feature = "instrumentation")]
    async fn execute_json<T>(&self, feature: &'static str, request: Request) -> HttpResult<T>
    where
        T: DeserializeOwned,
    {
        use opentelemetry::{
            global,
            trace::{TraceContextExt, Tracer},
        };

        let tracer = global::tracer("tunneling");
        let span = tracer.start(feature);
        let cx = opentelemetry::Context::current_with_span(span);
        let guard = cx.clone().attach();

        let res = self.execute_json_simple(request).await;
        if let Err(e) = &res {
            cx.span().record_exception(e);
        }

        drop(guard);

        res
    }

    /// Executes a request in which 200 status codes indicate success and
    /// 404 indicates an unsuccessful deletion but is not an error.
    async fn execute_no_response(&self, _: &'static str, request: Request) -> HttpResult<bool> {
        let url_clone = request.url().clone();
        let res = self
            .client
            .execute(request)
            .await
            .map_err(HttpError::ConnectionError)?;

        if res.status().is_success() {
            Ok(true)
        } else if res.status().as_u16() == 404 {
            Ok(false)
        } else {
            let request_id = res
                .headers()
                .get("VsSaaS-Request-Id")
                .and_then(|h| h.to_str().ok())
                .map(|s| s.to_owned());

            Err(HttpError::ResponseError(ResponseError {
                url: url_clone,
                status_code: res.status(),
                data: res.text().await.ok(),
                request_id,
            }))
        }
    }

    /// Sends the request and deserializes a JSON response
    #[cfg(not(feature = "instrumentation"))]
    async fn execute_json<T>(&self, _: &'static str, request: Request) -> HttpResult<T>
    where
        T: DeserializeOwned,
    {
        self.execute_json_simple(request).await
    }

    async fn execute_json_simple<T>(&self, request: Request) -> HttpResult<T>
    where
        T: DeserializeOwned,
    {
        let url_clone = request.url().clone();
        let res = self
            .client
            .execute(request)
            .await
            .map_err(HttpError::ConnectionError)?;

        if res.status().is_success() {
            res.json::<T>().await.map_err(HttpError::ConnectionError)
        } else {
            let request_id = res
                .headers()
                .get("VsSaaS-Request-Id")
                .and_then(|h| h.to_str().ok())
                .map(|s| s.to_owned());

            Err(HttpError::ResponseError(ResponseError {
                url: url_clone,
                status_code: res.status(),
                data: res.text().await.ok(),
                request_id,
            }))
        }
    }

    /// Builds a URI that does an operation on a tunnel.
    fn build_tunnel_uri(&self, locator: &TunnelLocator, path: Option<&str>) -> Url {
        let make_path = |ident: &str| {
            path.map(|p| format!("{}/{}/{}", TUNNELS_API_PATH, ident, p))
                .unwrap_or_else(|| format!("{}/{}", TUNNELS_API_PATH, ident))
        };

        match locator {
            TunnelLocator::Name(name) => self.build_uri(None, &make_path(name)),
            TunnelLocator::ID { cluster, id } => self.build_uri(Some(cluster), &make_path(id)),
        }
    }

    /// Builds a URI to a path on the given cluster, if given, or to the global
    /// service if nont is provided.
    fn build_uri(&self, cluster_id: Option<&str>, path: &str) -> Url {
        let mut uri =
            Url::parse(&self.environment.service_uri).expect("expected valid service_uri");

        if let Some(cluster_id) = cluster_id {
            let hostname = uri.host_str().unwrap_or("");
            if !hostname.starts_with(&format!("{}.", cluster_id)) {
                let new_hostname = format!("{}.{}", cluster_id, hostname).replace("global.", "");
                uri.set_host(Some(&new_hostname)).unwrap();
            }
        }

        uri.set_path(path);

        uri
    }

    /// Makes a request and applies the additional tunnel options to the headers and query string.
    async fn make_tunnel_request(
        &self,
        method: Method,
        mut url: Url,
        tunnel_opts: &TunnelRequestOptions,
    ) -> HttpResult<Request> {
        add_query(&mut url, tunnel_opts, &self.api_version);
        let mut request = self.make_request(method, url).await?;

        let headers = request.headers_mut();
        if let Some(authorization) = &tunnel_opts.authorization {
            if let Some(a) = authorization.as_header() {
                headers.insert(AUTHORIZATION, HeaderValue::from_str(&a).unwrap());
            } else {
                headers.remove(AUTHORIZATION);
            }
        }

        for (name, value) in &tunnel_opts.headers {
            headers.append(name, value.to_owned());
        }

        Ok(request)
    }

    /// Makes a basic request that communicates with the service.
    async fn make_request(&self, method: Method, url: Url) -> HttpResult<Request> {
        let mut request = Request::new(method, url);
        let headers = request.headers_mut();
        headers.insert("User-Agent", self.user_agent.clone());

        // Add Windows group policies to the header
        match get_policy_header_value() {
            Ok(Some(policy_header_value)) => {
                if let Ok(header_value) = HeaderValue::from_maybe_shared(policy_header_value) {
                    headers.insert("User-Agent-Policies", header_value);
                } else {
                    log::error!("Invalid header value");
                }
            }
            Ok(None) => {
                // No policies to add
            }
            Err(e) => {
                log::error!("Failed to get policy header value: {}", e);
            }
        }

        if let Some(a) = self.authorization.get_authorization().await?.as_header() {
            headers.insert(AUTHORIZATION, HeaderValue::from_str(&a).unwrap());
        }

        Ok(request)
    }

    fn generate_tunnel_id() -> String {
        const NOUNS: [&str; 16] = [
            "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "shoe", "plane",
            "chair", "book", "ocean", "lake", "river", "horse",
        ];
        const ADJECTIVES: [&str; 20] = [
            "fun",
            "happy",
            "interesting",
            "neat",
            "peaceful",
            "puzzled",
            "kind",
            "joyful",
            "new",
            "giant",
            "sneaky",
            "quick",
            "majestic",
            "jolly",
            "fancy",
            "tidy",
            "swift",
            "silent",
            "amusing",
            "spiffy",
        ];
        const TUNNEL_ID_CHARS: &str = "bcdfghjklmnpqrstvwxz0123456789";

        let mut rng = rand::thread_rng();
        let mut tunnel_id = String::new();
        tunnel_id.push_str(ADJECTIVES[rng.gen_range(0..ADJECTIVES.len())]);
        tunnel_id.push('-');
        tunnel_id.push_str(NOUNS[rng.gen_range(0..NOUNS.len())]);
        tunnel_id.push('-');

        for _ in 0..7 {
            tunnel_id.push(
                TUNNEL_ID_CHARS
                    .chars()
                    .nth(rng.gen_range(0..TUNNEL_ID_CHARS.len()))
                    .unwrap(),
            );
        }
        tunnel_id
    }
}

fn json_body<T>(request: &mut Request, body: T)
where
    T: Serialize,
{
    request
        .headers_mut()
        .insert(CONTENT_TYPE, HeaderValue::from_static("application/json"));
    *request.body_mut() = Some(serde_json::to_vec(&body).unwrap().into());
}

pub struct TunnelClientBuilder {
    authorization: Arc<Box<dyn AuthorizationProvider>>,
    client: Option<Client>,
    user_agent: HeaderValue,
    environment: TunnelServiceProperties,
    api_version: String,
}

/// Creates a new tunnel client builder. You can set options, then use `into()`
/// to get the client instance (or cast automatically).
pub fn new_tunnel_management(user_agent: &str) -> TunnelClientBuilder {
    let full_user_agent = create_full_user_agent(user_agent);

    TunnelClientBuilder {
        authorization: Arc::new(Box::new(super::StaticAuthorizationProvider(
            Authorization::Anonymous,
        ))),
        client: None,
        user_agent: HeaderValue::from_str(&full_user_agent).unwrap(),
        environment: env_production(),
        api_version: API_VERSIONS[0].to_owned(),
    }
}

fn create_full_user_agent(user_agent: &str) -> String {
    let pkg_version = PKG_VERSION.unwrap_or("unknown");
    let os = os_info::get();
    let os_info = format!("{}: {} {}", "OS", os.os_type(), os.version());

    let mut full_user_agent = format!(
        "{}{}{} ({}",
        user_agent, " Dev-Tunnels-Service-Rust-SDK/", pkg_version, os_info
    );

    #[cfg(windows)]
    {
        use winreg::enums::*;
        use winreg::RegKey;
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        let key = hklm.open_subkey("SOFTWARE\\Microsoft\\Windows365");
        if let Ok(id) = key.and_then(|k| k.get_value::<String, _>("PartnerId")) {
            full_user_agent.push_str("; Windows-Partner-Id: ");
            full_user_agent.push_str(&id);
        }
    }

    full_user_agent.push(')');

    full_user_agent
}

impl TunnelClientBuilder {
    pub fn authorization(&mut self, authorization: Authorization) -> &mut Self {
        self.authorization = Arc::new(Box::new(super::StaticAuthorizationProvider(authorization)));
        self
    }

    pub fn authorization_provider(
        &mut self,
        provider: impl AuthorizationProvider + 'static,
    ) -> &mut Self {
        self.authorization = Arc::new(Box::new(provider));
        self
    }

    pub fn client(&mut self, client: Client) -> &mut Self {
        self.client = Some(client);
        self
    }

    pub fn environment(&mut self, environment: TunnelServiceProperties) -> &mut Self {
        self.environment = environment;
        self
    }
}

impl From<TunnelClientBuilder> for TunnelManagementClient {
    fn from(builder: TunnelClientBuilder) -> Self {
        TunnelManagementClient {
            authorization: builder.authorization,
            client: builder.client.unwrap_or_default(),
            user_agent: builder.user_agent,
            environment: builder.environment,
            api_version: builder.api_version,
        }
    }
}

fn add_query(url: &mut Url, tunnel_opts: &TunnelRequestOptions, api_version: &str) {
    if tunnel_opts.include_ports {
        url.query_pairs_mut().append_pair("includePorts", "true");
    }
    if tunnel_opts.include_access_control {
        url.query_pairs_mut()
            .append_pair("includeAccessControl", "true");
    }
    if !tunnel_opts.token_scopes.is_empty() {
        url.query_pairs_mut()
            .append_pair("tokenScopes", &tunnel_opts.token_scopes.join(","));
    }
    if tunnel_opts.force_rename {
        url.query_pairs_mut().append_pair("forceRename", "true");
    }
    if !tunnel_opts.labels.is_empty() {
        url.query_pairs_mut()
            .append_pair("labels", &tunnel_opts.labels.join(","));
        if tunnel_opts.require_all_labels {
            url.query_pairs_mut().append_pair("allLabels", "true");
        }
    }
    url.query_pairs_mut()
        .append_pair("api-version", api_version);
    if tunnel_opts.limit > 0 {
        url.query_pairs_mut()
            .append_pair("limit", &tunnel_opts.limit.to_string());
    }
}

// End to end tests can be run with `cargo test --features end_to_end -- --nocapture`
// with an environment variable TUNNEL_TEST_CLIENT_ID.
#[cfg(test)]
#[cfg(feature = "end_to_end")]
mod test_end_to_end {
    use std::{env, time::Duration};

    use async_trait::async_trait;
    use serde::Deserialize;
    use tokio::time::sleep;

    use crate::{
        contracts::{Tunnel, PROD_FIRST_PARTY_APP_ID},
        management::{
            Authorization, AuthorizationProvider, HttpError, TunnelLocator, NO_REQUEST_OPTIONS,
        },
    };

    use super::{new_tunnel_management, TunnelManagementClient};

    #[tokio::test]
    async fn round_trips_tunnel() {
        let c = get_client().await;

        // create tunnel
        let tunnel = c
            .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
            .await
            .unwrap();
        assert!(tunnel.tunnel_id.is_some());
        let ident = TunnelLocator::try_from(&tunnel).unwrap();

        // get tunnel
        let tunnel2 = c.get_tunnel(&ident, NO_REQUEST_OPTIONS).await.unwrap();
        assert_eq!(tunnel.tunnel_id, tunnel2.tunnel_id);

        // appears in list tunnels
        let tunnels = c.list_all_tunnels(NO_REQUEST_OPTIONS).await.unwrap();
        assert!(tunnels
            .iter()
            .find(|t| t.tunnel_id == tunnel.tunnel_id)
            .is_some());

        // delete tunnel
        c.delete_tunnel(&ident, NO_REQUEST_OPTIONS).await.unwrap();
    }

    #[derive(Deserialize)]
    struct DeviceCodeResponse {
        device_code: String,
        message: String,
    }

    #[derive(Deserialize)]
    struct AuthenticationResponse {
        access_token: String,
    }

    async fn do_device_code_flow(client: &reqwest::Client) -> String {
        let client_id = match env::var("TUNNEL_TEST_CLIENT_ID") {
            Ok(value) => value,
            _ => panic!("TUNNEL_TEST_CLIENT_ID must be set"),
        };

        let base_uri = "https://login.microsoftonline.com/organizations/oauth2/v2.0";
        let verification = client
            .post(format!("{}/devicecode", base_uri))
            .body(format!(
                "client_id={}&scope={}/.default",
                client_id, PROD_FIRST_PARTY_APP_ID
            ))
            .send()
            .await
            .unwrap()
            .json::<DeviceCodeResponse>()
            .await
            .unwrap();

        println!("{}", verification.message);

        loop {
            sleep(Duration::from_secs(5)).await;

            let response = client.post(format!("{}/token", base_uri))
                .body(format!(
                    "client_id={}&grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code={}",
                    client_id, verification.device_code
                ))
                .send()
                .await
                .unwrap();
            if !response.status().is_success() {
                continue;
            }

            let body = response.json::<AuthenticationResponse>().await.unwrap();

            println!("accessToken is {}", body.access_token);
            println!(
                "You can save this in the TUNNEL_TEST_AAD_TOKEN environment variable for next time"
            );

            return body.access_token;
        }
    }

    struct AuthCodeProvider();

    #[async_trait]
    impl AuthorizationProvider for AuthCodeProvider {
        async fn get_authorization(&self) -> Result<Authorization, HttpError> {
            let token = match env::var("TUNNEL_TEST_AAD_TOKEN") {
                Ok(value) => value,
                _ => do_device_code_flow(&reqwest::Client::new()).await,
            };

            env::set_var("TUNNEL_TEST_AAD_TOKEN", &token);
            Ok(Authorization::Bearer(token))
        }
    }

    async fn get_client() -> TunnelManagementClient {
        let mut c = new_tunnel_management("rs-sdk-tests");
        c.authorization_provider(AuthCodeProvider());
        c.into()
    }
}

#[cfg(test)]
mod tests {
    use regex::Regex;
    use reqwest::Url;

    use crate::management::NO_REQUEST_OPTIONS;

    #[test]
    fn new_tunnel_management_has_user_agent() {
        // test
        let builder = super::new_tunnel_management("test-caller");

        // verify
        let re = Regex::new(r"^test-caller Dev-Tunnels-Service-Rust-SDK/[0-9]+\.[0-9]+\.[0-9]+.*$")
            .unwrap();
        let full_agent = builder.user_agent.to_str().unwrap();
        assert!(re.is_match(full_agent));
    }

    #[test]
    fn add_query_omits_empty_query() {
        let mut url = Url::parse("https://tunnels.api.visualstudio.com/api/v1/tunnels").unwrap();
        let options = NO_REQUEST_OPTIONS;

        super::add_query(&mut url, options, "2023-09-27-preview");

        assert!(!url.to_string().ends_with('?'));
    }

    #[test]
    fn add_query_adds_ports() {
        let mut url = Url::parse("https://tunnels.api.visualstudio.com/api/v1/tunnels").unwrap();
        let mut options = NO_REQUEST_OPTIONS.clone();
        options.include_ports = true;

        super::add_query(&mut url, &options, "2023-09-27-preview");

        assert!(url.query().unwrap().contains("includePorts=true"));
    }
}
