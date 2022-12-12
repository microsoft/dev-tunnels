// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelOptions.cs

use serde::{Deserialize, Serialize};

// Data contract for `Tunnel` or `TunnelPort` options.
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all(serialize = "camelCase", deserialize = "camelCase"))]
pub struct TunnelOptions {
    // Gets or sets a value indicating whether web-forwarding of this tunnel can run on
    // any cluster (region) without redirecting to the home cluster. This is only
    // applicable if the tunnel has a name and web-forwarding uses it.
    #[serde(default)]
    pub is_globally_available: bool,

    // Gets or sets a value for `Host` header rewriting to use in web-forwarding of this
    // tunnel or port. By default, with this property null or empty, web-forwarding uses
    // "localhost" to rewrite the header. Web-fowarding will use this property instead if
    // it is not null or empty. Port-level option, if set, takes precedence over this
    // option on the tunnel level. The option is ignored if IsHostHeaderUnchanged is true.
    #[serde(default)]
    pub host_header: Option<String>,

    // Gets or sets a value indicating whether `Host` header is rewritten or the header
    // value stays intact. By default, if false, web-forwarding rewrites the host header
    // with the value from HostHeader property or "localhost". If true, the host header
    // will be whatever the tunnel's web-forwarding host is, e.g.
    // tunnel-name-8080.devtunnels.ms. Port-level option, if set, takes precedence over
    // this option on the tunnel level.
    #[serde(default)]
    pub is_host_header_unchanged: bool,

    // Gets or sets a value for `Origin` header rewriting to use in web-forwarding of this
    // tunnel or port. By default, with this property null or empty, web-forwarding uses
    // "http(s)://localhost" to rewrite the header. Web-fowarding will use this property
    // instead if it is not null or empty. Port-level option, if set, takes precedence
    // over this option on the tunnel level. The option is ignored if
    // IsOriginHeaderUnchanged is true.
    #[serde(default)]
    pub origin_header: Option<String>,

    // Gets or sets a value indicating whether `Origin` header is rewritten or the header
    // value stays intact. By default, if false, web-forwarding rewrites the origin header
    // with the value from OriginHeader property or  "http(s)://localhost". If true, the
    // Origin header will be whatever the tunnel's web-forwarding Origin is, e.g.
    // https://tunnel-name-8080.devtunnels.ms. Port-level option, if set, takes precedence
    // over this option on the tunnel level.
    #[serde(default)]
    pub is_origin_header_unchanged: bool,

    // Gets or sets if inspection is enabled for the tunnel.
    #[serde(default)]
    pub is_inspection_enabled: bool,
}
