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
    ///
    /// Ports are excluded by default when retrieving a tunnel or when listing or searching
    /// tunnels. This option enables including ports for all tunnels returned by a list or
    /// search query.
    pub include_ports: bool,

    /// Gets or sets a flag that requests access control details when retrieving tunnels.
    ///
    /// Access control details are always included when retrieving a single tunnel,
    /// but excluded by default when listing or searching tunnels. This option enables
    /// including access controls for all tunnels returned by a list or search query.
    pub include_access_control: bool,

    /// Gets or sets an optional list of labels to filter the requested tunnels or ports.
    ///
    /// Requested labels are compared to the `Tunnel.labels` or `TunnelPort.labels` when calling
    /// `TunnelManagementClient.list_all_tunnels` or `TunnelManagementClient.list_tunnel_ports`
    /// respectively. By default, an item is included if ANY label matches; set `require_all_labels`
    /// to match ALL labels instead.
    pub labels: Vec<String>,

    /// Gets or sets a flag that indicates whether listed items must match all labels
    /// specified in `labels`. If false, an item is included if any labels matches.
    pub require_all_labels: bool,

    /// Gets or sets an optional list of token scopes that
    /// are requested when retrieving a tunnel or tunnel port object.
    pub token_scopes: Vec<String>,

    /// If true on a create or update request then upon a name conflict, attempt to rename the
    /// existing tunnel to null and give the name to the tunnel from the request.
    pub force_rename: bool,

    /// Limits the number of tunnels returned when searching or listing tunnels.
    pub limit: u32,
}

pub const NO_REQUEST_OPTIONS: &TunnelRequestOptions = &TunnelRequestOptions {
    authorization: None,
    headers: Vec::new(),
    include_ports: false,
    include_access_control: false,
    labels: Vec::new(),
    require_all_labels: false,
    token_scopes: Vec::new(),
    force_rename: false,
    limit: 0,
};
