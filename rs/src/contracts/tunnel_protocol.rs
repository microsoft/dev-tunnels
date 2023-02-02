// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelProtocol.cs

// Defines possible values for the protocol of a `TunnelPort`.

// The protocol is automatically detected. (TODO: Define detection semantics.)
pub const TUNNEL_PROTOCOL_AUTO: &str = r#"auto"#;

// Unknown TCP protocol.
pub const TUNNEL_PROTOCOL_TCP: &str = r#"tcp"#;

// Unknown UDP protocol.
pub const TUNNEL_PROTOCOL_UDP: &str = r#"udp"#;

// SSH protocol.
pub const TUNNEL_PROTOCOL_SSH: &str = r#"ssh"#;

// Remote desktop protocol.
pub const TUNNEL_PROTOCOL_RDP: &str = r#"rdp"#;

// HTTP protocol.
pub const TUNNEL_PROTOCOL_HTTP: &str = r#"http"#;

// HTTPS protocol.
pub const TUNNEL_PROTOCOL_HTTPS: &str = r#"https"#;
