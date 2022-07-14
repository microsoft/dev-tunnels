use reqwest::header::{HeaderName, HeaderValue};

use super::Authorization;

#[derive(Default, Clone)]
pub struct TunnelRequestOptions {
    /// Gets or sets authorization for the request.
    ///
    /// Note this should not be a _user_ access token (such as AAD or GitHub); use the
    /// callback parameter to the `TunnelManagementClient` constructor to
    /// supply user access tokens.
    pub authorization: Option<Authorization>,

    /// Gets or sets additional headers to be included in the request.
    pub headers: Vec<(HeaderName, HeaderValue)>,

    /// Gets or sets a flag that requests tunnel ports when retrieving a tunnel object.
    pub include_ports: bool,

    /// Gets or sets an optional list of tags to filter the requested tunnels or ports.
    ///
    /// Requested tags are compared to the `Tunnel.tags` or `TunnelPort.tags` when calling
    /// `TunnelManagementClient.list_all_tunnels` or `TunnelManagementClient.list_tunnel_ports`
    /// respectively. By default, an item is included if ANY tag matches; set `require_all_tags`
    /// to match ALL tags instead.
    pub tags: Vec<String>,

    /// Gets or sets a flag that indicates whether listed items must match all tags
    /// specified in `tags`. If false, an item is included if any tag matches.
    pub require_all_tags: bool,

    /// Gets or sets an optional list of token scopes that
    /// are requested when retrieving a tunnel or tunnel port object.
    pub token_scopes: Vec<String>,

    /// Gets or sets an optional list of scopes that should be authorized when
    /// retrieving a tunnel or tunnel port object.
    pub scopes: Vec<String>,

    /// If true on a create or update request then upon a name conflict, attempt to rename the
    /// existing tunnel to null and give the name to the tunnel from the request.
    pub force_rename: bool,
}

pub const NO_REQUEST_OPTIONS: &TunnelRequestOptions = &TunnelRequestOptions {
    authorization: None,
    headers: Vec::new(),
    include_ports: false,
    tags: Vec::new(),
    require_all_tags: false,
    token_scopes: Vec::new(),
    scopes: Vec::new(),
    force_rename: false,
};
