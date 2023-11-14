// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelHeaderNames.cs

// Header names for http requests that Tunnel Service can handle

// Additional authorization header that can be passed to tunnel web forwarding to
// authenticate and authorize the client. The format of the value is the same as
// Authorization header that is sent to the Tunnel service by the tunnel SDK. Supported
// schemes: "tunnel" with the tunnel access JWT good for 'Connect' scope.
pub const X_TUNNEL_AUTHORIZATION: &str = r#"X-Tunnel-Authorization"#;

// Request ID header that nginx ingress controller adds to all requests if it's not there.
pub const X_REQUEST_ID: &str = r#"X-Request-ID"#;

// Github Ssh public key which can be used to validate if it belongs to tunnel's owner.
pub const X_GITHUB_SSH_KEY: &str = r#"X-Github-Ssh-Key"#;

// Header that will skip the antiphishing page when connection to a tunnel through web
// forwarding.
pub const X_TUNNEL_SKIP_ANTIPHISHING_PAGE: &str = r#"X-Tunnel-Skip-AntiPhishing-Page"#;
