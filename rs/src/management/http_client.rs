// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::sync::Arc;

use reqwest::{
    header::{HeaderName, HeaderValue, AUTHORIZATION, CONTENT_TYPE},
    Client, Method, Request,
};

use serde::{de::DeserializeOwned, Serialize};
use url::Url;

use rand::Rng;

use crate::contracts::{
    env_production, ClusterRecommendationResponse, NamedRateStatus, Tunnel, TunnelEndpoint,
    TunnelListByRegionResponse, TunnelPort, TunnelPortListResponse, TunnelServiceProperties,
};

use super::{
    Authorization, AuthorizationProvider, HttpError, HttpResult, ResponseError,
    TunnelClusterSource, TunnelLocator, TunnelRequestOptions, NO_REQUEST_OPTIONS,
};

use crate::management::policy_provider::get_policy_header_value;

#[derive(Clone)]
pub struct TunnelManagementClient {
    client: Client,
    authorization: Arc<Box<dyn AuthorizationProvider>>,
    pub(crate) user_agent: HeaderValue,
    environment: TunnelServiceProperties,
    api_version: String,
    is_custom_domain: bool,
}

const TUNNELS_API_PATH: &str = "/tunnels";
const CLUSTERS_API_PATH: &str = "/clusters";
const RECOMMENDATIONS_API_SUB_PATH: &str = "/recommendations";
const USER_LIMITS_API_PATH: &str = "/userlimits";
const ENDPOINTS_API_SUB_PATH: &str = "endpoints";
const PORTS_API_SUB_PATH: &str = "ports";
const CHECK_TUNNEL_NAME_SUB_PATH: &str = ":checkNameAvailability";
const CLUSTER_SOURCE_HEADER_NAME: &str = "x-tunnel-cluster-source";
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
            is_custom_domain: self.is_custom_domain,
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
        let cluster_is_unspecified = match tunnel.cluster_id.as_deref() {
            Some(cluster_id) => cluster_id.is_empty(),
            None => true,
        };
        let cluster_source = if cluster_is_unspecified {
            // An empty cluster is equivalent to no cluster and must use global routing if
            // recommendation does not return one.
            tunnel.cluster_id = None;
            match self.get_cluster_recommendations_internal(None, None).await {
                Ok(recommendations) => {
                    if let Some(cluster_id) = recommendations
                        .response
                        .recommended_cluster_id
                        .filter(|cluster_id| !cluster_id.is_empty())
                    {
                        tunnel.cluster_id = Some(cluster_id);
                        if recommendations.auth_rejected {
                            TunnelClusterSource::RecommendedAfterAuthRejected
                        } else {
                            TunnelClusterSource::Recommended
                        }
                    } else {
                        TunnelClusterSource::FallbackEmpty
                    }
                }
                Err(error) if is_unauthorized(&error) => TunnelClusterSource::FallbackAuthFailed,
                Err(_) => TunnelClusterSource::FallbackError,
            }
        } else {
            TunnelClusterSource::Explicit
        };

        let tunnel_id = tunnel
            .tunnel_id
            .take()
            .unwrap_or_else(TunnelManagementClient::generate_tunnel_id);

        let mut url = self.build_uri(tunnel.cluster_id.as_deref(), TUNNELS_API_PATH);
        let new_path = url.path().to_owned() + "/" + &tunnel_id;
        url.set_path(&new_path);
        tunnel.tunnel_id = Some(tunnel_id);

        let mut request = self.make_tunnel_request(Method::PUT, url, options).await?;
        add_cluster_source_header(&mut request, cluster_source);
        json_body(&mut request, tunnel);
        self.execute_json("create_tunnel", request).await
    }

    /// Gets cluster recommendations for tunnel creation based on capacity and availability.
    #[allow(clippy::result_large_err)]
    pub async fn get_cluster_recommendations(
        &self,
        preferred_cluster_id: Option<&str>,
        required_geo: Option<&str>,
    ) -> HttpResult<ClusterRecommendationResponse> {
        Ok(self
            .get_cluster_recommendations_internal(preferred_cluster_id, required_geo)
            .await?
            .response)
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
            Some(&format!(
                "{}/{}",
                ENDPOINTS_API_SUB_PATH,
                endpoint.id.as_deref().unwrap()
            )),
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
        endpoint: &TunnelEndpoint,
        options: &TunnelRequestOptions,
    ) -> HttpResult<TunnelEndpoint> {
        let mut url = self.build_tunnel_uri(
            locator,
            Some(&format!(
                "{}/{}",
                ENDPOINTS_API_SUB_PATH,
                endpoint.id.as_deref().unwrap()
            )),
        );
        url.query_pairs_mut()
            .append_pair("connectionMode", &endpoint.connection_mode.to_string());
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

    /// Requests cluster recommendations and reports whether the provider authorization was rejected.
    #[allow(clippy::result_large_err)]
    async fn get_cluster_recommendations_internal(
        &self,
        preferred_cluster_id: Option<&str>,
        required_geo: Option<&str>,
    ) -> HttpResult<ClusterRecommendationsResult> {
        let authorization = self.authorization.get_authorization().await?;
        let authorization_sent = authorization.as_header().is_some();

        match self
            .send_cluster_recommendations_request(
                preferred_cluster_id,
                required_geo,
                &authorization,
            )
            .await
        {
            Ok(response) => Ok(ClusterRecommendationsResult {
                response,
                auth_rejected: false,
            }),
            Err(error) if authorization_sent && is_unauthorized(&error) => {
                let response = self
                    .send_cluster_recommendations_request(
                        preferred_cluster_id,
                        required_geo,
                        &Authorization::Anonymous,
                    )
                    .await?;
                Ok(ClusterRecommendationsResult {
                    response,
                    auth_rejected: true,
                })
            }
            Err(error) => Err(error),
        }
    }

    #[allow(clippy::result_large_err)]
    async fn send_cluster_recommendations_request(
        &self,
        preferred_cluster_id: Option<&str>,
        required_geo: Option<&str>,
        authorization: &Authorization,
    ) -> HttpResult<ClusterRecommendationResponse> {
        let url = self.build_cluster_recommendations_uri(preferred_cluster_id, required_geo);
        let request = self.make_tunnel_request_with_authorization(
            Method::GET,
            url,
            NO_REQUEST_OPTIONS,
            authorization,
        );
        self.execute_json("get_cluster_recommendations", request)
            .await
    }

    fn build_cluster_recommendations_uri(
        &self,
        preferred_cluster_id: Option<&str>,
        required_geo: Option<&str>,
    ) -> Url {
        let mut url = self.build_uri(
            None,
            &format!("{}{}", CLUSTERS_API_PATH, RECOMMENDATIONS_API_SUB_PATH),
        );
        {
            let mut query = url.query_pairs_mut();
            if let Some(preferred_cluster_id) =
                preferred_cluster_id.filter(|value| !value.is_empty())
            {
                query.append_pair("preferredClusterId", preferred_cluster_id);
            }
            if let Some(required_geo) = required_geo.filter(|value| !value.is_empty()) {
                query.append_pair("requiredGeo", required_geo);
            }
        }
        url
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
            if !self.is_custom_domain && !hostname.starts_with(&format!("{}.", cluster_id)) {
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
        url: Url,
        tunnel_opts: &TunnelRequestOptions,
    ) -> HttpResult<Request> {
        let authorization = match &tunnel_opts.authorization {
            Some(authorization) => authorization.clone(),
            None => self.authorization.get_authorization().await?,
        };
        Ok(self.make_tunnel_request_with_authorization(method, url, tunnel_opts, &authorization))
    }

    fn make_tunnel_request_with_authorization(
        &self,
        method: Method,
        mut url: Url,
        tunnel_opts: &TunnelRequestOptions,
        authorization: &Authorization,
    ) -> Request {
        add_query(&mut url, tunnel_opts, &self.api_version);
        let mut request = self.make_request(method, url);

        let headers = request.headers_mut();
        if let Some(a) = authorization.as_header() {
            headers.insert(AUTHORIZATION, HeaderValue::from_str(&a).unwrap());
        } else {
            headers.remove(AUTHORIZATION);
        }

        for (name, value) in &tunnel_opts.headers {
            headers.append(name, value.to_owned());
        }

        request
    }

    /// Makes a basic request that communicates with the service.
    fn make_request(&self, method: Method, url: Url) -> Request {
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

        request
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

struct ClusterRecommendationsResult {
    response: ClusterRecommendationResponse,
    auth_rejected: bool,
}

fn is_unauthorized(error: &HttpError) -> bool {
    matches!(
        error,
        HttpError::ResponseError(ResponseError {
            status_code,
            ..
        }) if status_code.as_u16() == 401 || status_code.as_u16() == 403
    )
}

fn add_cluster_source_header(request: &mut Request, cluster_source: TunnelClusterSource) {
    let header_name = HeaderName::from_static(CLUSTER_SOURCE_HEADER_NAME);
    let headers = request.headers_mut();
    headers.remove(&header_name);
    headers.append(
        header_name,
        HeaderValue::from_static(cluster_source.as_header_value()),
    );
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
    is_custom_domain: bool,
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
        is_custom_domain: false,
    }
}

/// Creates a new tunnel client builder configured for a custom domain.
/// When a custom domain is configured (e.g., "app.github.dev"), control plane calls
/// are routed to "cp.{domain}" and cluster ID hostname manipulation is skipped.
pub fn new_tunnel_management_for_custom_domain(
    user_agent: &str,
    custom_domain: &str,
) -> TunnelClientBuilder {
    let mut builder = new_tunnel_management(user_agent);
    builder.environment = TunnelServiceProperties {
        service_uri: format!("https://cp.{}", custom_domain),
        service_app_id: String::new(),
        service_internal_app_id: String::new(),
        github_app_client_id: String::new(),
    };
    builder.is_custom_domain = true;
    builder
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
        self.is_custom_domain = Url::parse(&environment.service_uri)
            .ok()
            .and_then(|u| u.host_str().map(|h| h.starts_with("cp.")))
            .unwrap_or(false);
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
            is_custom_domain: builder.is_custom_domain,
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

    use serde::Deserialize;
    use tokio::time::sleep;

    use crate::{
        contracts::{Tunnel, PROD_FIRST_PARTY_APP_ID},
        management::{
            Authorization, AuthorizationProvider, BoxFuture, HttpError, TunnelLocator,
            NO_REQUEST_OPTIONS,
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

    impl AuthorizationProvider for AuthCodeProvider {
        fn get_authorization(&self) -> BoxFuture<'_, Result<Authorization, HttpError>> {
            Box::pin(async {
                let token = match env::var("TUNNEL_TEST_AAD_TOKEN") {
                    Ok(value) => value,
                    _ => do_device_code_flow(&reqwest::Client::new()).await,
                };

                env::set_var("TUNNEL_TEST_AAD_TOKEN", &token);
                Ok(Authorization::Bearer(token))
            })
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
    use std::{
        net::SocketAddr,
        sync::{Arc, Mutex},
        time::Duration,
    };

    use regex::Regex;
    use reqwest::Url;
    use tokio::{
        io::{AsyncReadExt, AsyncWriteExt},
        net::{TcpListener, TcpStream},
        task::JoinHandle,
        time::timeout,
    };

    use crate::{
        contracts::{Tunnel, TunnelServiceProperties},
        management::{Authorization, TunnelRequestOptions, NO_REQUEST_OPTIONS},
    };

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

    #[test]
    fn custom_domain_does_not_modify_hostname() {
        let builder =
            super::new_tunnel_management_for_custom_domain("rs-sdk-tests", "app.github.dev");
        let client: super::TunnelManagementClient = builder.into();
        let url = client.build_uri(Some("usw2"), "/tunnels/tnnl0001");
        assert_eq!(url.host_str().unwrap(), "cp.app.github.dev");
    }

    #[test]
    fn standard_service_uri_replaces_cluster_id() {
        let builder = super::new_tunnel_management("rs-sdk-tests");
        let client: super::TunnelManagementClient = builder.into();
        let url = client.build_uri(Some("usw2"), "/tunnels/tnnl0001");
        assert!(
            url.host_str().unwrap().starts_with("usw2."),
            "Expected hostname to start with usw2., got {}",
            url.host_str().unwrap()
        );
    }

    #[tokio::test]
    async fn get_cluster_recommendations_includes_optional_query_parameters() {
        let server = TestServer::start(vec![TestResponse::ok(recommendation_response(Some(
            "recommended",
        )))])
        .await;
        let client = test_client(&server, Authorization::Anonymous);

        let response = client
            .get_cluster_recommendations(Some("preferred cluster"), Some("eu west"))
            .await
            .unwrap();

        assert_eq!(
            response.recommended_cluster_id.as_deref(),
            Some("recommended")
        );
        let requests = server.finish().await;
        assert_eq!(requests.len(), 1);
        assert!(requests[0].target.contains("preferredClusterId=preferred"));
        assert!(requests[0].target.contains("requiredGeo=eu"));
        assert_eq!(requests[0].target.matches("api-version=").count(), 1);
    }

    #[tokio::test]
    async fn create_uses_authenticated_recommendation() {
        let server = TestServer::start(vec![
            TestResponse::ok(recommendation_response(Some("recommended"))),
            TestResponse::ok(tunnel_response("recommended")),
        ])
        .await;
        let client = test_client(&server, Authorization::Bearer("token".to_owned()));

        let tunnel = client
            .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
            .await
            .unwrap();

        assert_eq!(tunnel.cluster_id.as_deref(), Some("recommended"));
        let requests = server.finish().await;
        assert_eq!(requests.len(), 2);
        assert!(requests[0].target.starts_with("/clusters/recommendations?"));
        assert_eq!(requests[0].header_values("authorization"), ["bearer token"]);
        assert!(requests[1].target.starts_with("/tunnels/"));
        assert!(requests[1]
            .host
            .as_deref()
            .map_or(false, |host| host.starts_with("recommended.localhost")));
        assert_eq!(
            requests[1].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["recommended"]
        );
    }

    #[tokio::test]
    async fn create_retries_unauthorized_recommendation_anonymously() {
        for status in [401, 403] {
            let server = TestServer::start(vec![
                TestResponse::status(status),
                TestResponse::ok(recommendation_response(Some("recommended"))),
                TestResponse::ok(tunnel_response("recommended")),
            ])
            .await;
            let client = test_client(&server, Authorization::Bearer("token".to_owned()));

            client
                .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
                .await
                .unwrap();

            let requests = server.finish().await;
            assert_eq!(requests.len(), 3, "status {status}");
            assert_eq!(
                requests[0].header_values("authorization"),
                ["bearer token"],
                "status {status}"
            );
            assert!(
                requests[1].header_values("authorization").is_empty(),
                "status {status}"
            );
            assert_eq!(
                requests[1].target.matches("api-version=").count(),
                1,
                "status {status}"
            );
            assert_eq!(
                requests[2].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
                ["recommended-after-auth-rejected"],
                "status {status}"
            );
        }
    }

    #[tokio::test]
    async fn create_does_not_retry_non_auth_recommendation_failure() {
        let server = TestServer::start(vec![
            TestResponse::status(500),
            TestResponse::ok(tunnel_response("global")),
        ])
        .await;
        let client = test_client(&server, Authorization::Bearer("token".to_owned()));

        client
            .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
            .await
            .unwrap();

        let requests = server.finish().await;
        assert_eq!(requests.len(), 2);
        assert!(requests[1].target.starts_with("/tunnels/"));
        assert!(requests[1]
            .host
            .as_deref()
            .map_or(false, |host| host.starts_with("localhost")));
        assert_eq!(
            requests[1].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["fallback-error"]
        );
    }

    #[tokio::test]
    async fn create_reports_auth_failure_after_anonymous_retry_is_rejected() {
        let server = TestServer::start(vec![
            TestResponse::status(401),
            TestResponse::status(403),
            TestResponse::ok(tunnel_response("global")),
        ])
        .await;
        let client = test_client(&server, Authorization::Bearer("token".to_owned()));

        client
            .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
            .await
            .unwrap();

        let requests = server.finish().await;
        assert_eq!(requests.len(), 3);
        assert!(requests[1].header_values("authorization").is_empty());
        assert_eq!(
            requests[2].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["fallback-auth-failed"]
        );
    }

    #[tokio::test]
    async fn create_reports_empty_recommendation() {
        let server = TestServer::start(vec![
            TestResponse::ok(recommendation_response(None)),
            TestResponse::ok(tunnel_response("global")),
        ])
        .await;
        let client = test_client(&server, Authorization::Anonymous);

        client
            .create_tunnel(
                Tunnel {
                    cluster_id: Some(String::new()),
                    ..Tunnel::default()
                },
                NO_REQUEST_OPTIONS,
            )
            .await
            .unwrap();

        let requests = server.finish().await;
        assert_eq!(requests.len(), 2);
        assert_eq!(
            requests[1].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["fallback-empty"]
        );
    }

    #[tokio::test]
    async fn create_does_not_retry_anonymous_recommendation() {
        let server = TestServer::start(vec![
            TestResponse::status(401),
            TestResponse::ok(tunnel_response("global")),
        ])
        .await;
        let client = test_client(&server, Authorization::Anonymous);

        client
            .create_tunnel(Tunnel::default(), NO_REQUEST_OPTIONS)
            .await
            .unwrap();

        let requests = server.finish().await;
        assert_eq!(requests.len(), 2);
        assert_eq!(
            requests[1].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["fallback-auth-failed"]
        );
    }

    #[tokio::test]
    async fn create_explicit_cluster_bypasses_recommendations_and_prevents_source_spoofing() {
        let server = TestServer::start(vec![TestResponse::ok(tunnel_response("explicit"))]).await;
        let client = test_client(&server, Authorization::Anonymous);
        let options = TunnelRequestOptions {
            headers: vec![
                (
                    reqwest::header::HeaderName::from_static("x-caller-header"),
                    reqwest::header::HeaderValue::from_static("caller"),
                ),
                (
                    reqwest::header::HeaderName::from_static(super::CLUSTER_SOURCE_HEADER_NAME),
                    reqwest::header::HeaderValue::from_static("spoofed"),
                ),
            ],
            ..TunnelRequestOptions::default()
        };

        client
            .create_tunnel(
                Tunnel {
                    cluster_id: Some("explicit".to_owned()),
                    ..Tunnel::default()
                },
                &options,
            )
            .await
            .unwrap();

        let requests = server.finish().await;
        assert_eq!(requests.len(), 1);
        assert!(requests[0].target.starts_with("/tunnels/"));
        assert!(requests[0]
            .host
            .as_deref()
            .map_or(false, |host| host.starts_with("explicit.localhost")));
        assert_eq!(requests[0].header_values("x-caller-header"), ["caller"]);
        assert_eq!(
            requests[0].header_values(super::CLUSTER_SOURCE_HEADER_NAME),
            ["explicit"]
        );
    }

    fn recommendation_response(recommended_cluster_id: Option<&str>) -> String {
        let recommended_cluster_id = recommended_cluster_id
            .map(|value| format!("\"{value}\""))
            .unwrap_or_else(|| "null".to_owned());
        format!(
            "{{\"recommendedClusterId\":{recommended_cluster_id},\"isFallback\":false,\"recommendations\":[]}}"
        )
    }

    fn tunnel_response(cluster_id: &str) -> String {
        format!("{{\"tunnelId\":\"created\",\"clusterId\":\"{cluster_id}\"}}")
    }

    fn test_client(
        server: &TestServer,
        authorization: Authorization,
    ) -> super::TunnelManagementClient {
        let http_client = reqwest::Client::builder()
            .resolve("recommended.localhost", server.address)
            .resolve("explicit.localhost", server.address)
            .build()
            .unwrap();
        let mut builder = super::new_tunnel_management("rs-sdk-tests");
        builder
            .client(http_client)
            .environment(TunnelServiceProperties {
                service_uri: format!("http://localhost:{}/", server.address.port()),
                service_app_id: String::new(),
                service_internal_app_id: String::new(),
                github_app_client_id: String::new(),
            })
            .authorization(authorization);
        builder.into()
    }

    struct TestServer {
        address: SocketAddr,
        requests: Arc<Mutex<Vec<RecordedRequest>>>,
        task: JoinHandle<()>,
    }

    impl TestServer {
        async fn start(responses: Vec<TestResponse>) -> Self {
            let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
            let address = listener.local_addr().unwrap();
            let requests = Arc::new(Mutex::new(Vec::new()));
            let recorded_requests = requests.clone();
            let task = tokio::spawn(async move {
                for response in responses {
                    let (mut stream, _) = listener.accept().await.unwrap();
                    let request = read_request(&mut stream).await;
                    recorded_requests.lock().unwrap().push(request);
                    let response = response.as_http();
                    stream.write_all(response.as_bytes()).await.unwrap();
                }
            });

            Self {
                address,
                requests,
                task,
            }
        }

        async fn finish(mut self) -> Vec<RecordedRequest> {
            if timeout(Duration::from_secs(1), &mut self.task)
                .await
                .is_err()
            {
                self.task.abort();
                let _ = self.task.await;
            }
            Arc::try_unwrap(self.requests)
                .unwrap()
                .into_inner()
                .unwrap()
        }
    }

    struct TestResponse {
        status: u16,
        body: String,
    }

    impl TestResponse {
        fn ok(body: String) -> Self {
            Self { status: 200, body }
        }

        fn status(status: u16) -> Self {
            Self {
                status,
                body: "{}".to_owned(),
            }
        }

        fn as_http(&self) -> String {
            format!(
                "HTTP/1.1 {} Test\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                self.status,
                self.body.len(),
                self.body
            )
        }
    }

    #[derive(Debug)]
    struct RecordedRequest {
        target: String,
        host: Option<String>,
        headers: Vec<(String, String)>,
    }

    impl RecordedRequest {
        fn header_values(&self, name: &str) -> Vec<&str> {
            self.headers
                .iter()
                .filter_map(|(header_name, value)| (header_name == name).then_some(value.as_str()))
                .collect()
        }
    }

    async fn read_request(stream: &mut TcpStream) -> RecordedRequest {
        let mut data = Vec::new();
        let mut buffer = [0; 1024];
        let header_end;
        loop {
            let count = stream.read(&mut buffer).await.unwrap();
            assert_ne!(count, 0, "connection closed before request headers");
            data.extend_from_slice(&buffer[..count]);
            if let Some(end) = data.windows(4).position(|window| window == b"\r\n\r\n") {
                header_end = end + 4;
                break;
            }
        }

        let headers = std::str::from_utf8(&data[..header_end]).unwrap();
        let mut lines = headers.split("\r\n");
        let target = lines
            .next()
            .unwrap()
            .split_whitespace()
            .nth(1)
            .unwrap()
            .to_owned();
        let headers: Vec<(String, String)> = lines
            .filter_map(|line| line.split_once(':'))
            .map(|(name, value)| (name.to_ascii_lowercase(), value.trim().to_owned()))
            .collect();
        let content_length = headers
            .iter()
            .find_map(|(name, value)| (name == "content-length").then_some(value))
            .and_then(|value| value.parse::<usize>().ok())
            .unwrap_or(0);
        while data.len() < header_end + content_length {
            let count = stream.read(&mut buffer).await.unwrap();
            assert_ne!(count, 0, "connection closed before request body");
            data.extend_from_slice(&buffer[..count]);
        }

        RecordedRequest {
            host: headers
                .iter()
                .find_map(|(name, value)| (name == "host").then_some(value.clone())),
            target,
            headers,
        }
    }
}
