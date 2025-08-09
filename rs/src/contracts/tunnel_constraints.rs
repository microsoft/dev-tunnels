// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConstraints.cs

// Tunnel constraints.

// Min length of tunnel cluster ID.
pub const CLUSTER_ID_MIN_LENGTH: i32 = 3;

// Max length of tunnel cluster ID.
pub const CLUSTER_ID_MAX_LENGTH: i32 = 12;

// Length of V1 tunnel id.
pub const OLD_TUNNEL_ID_LENGTH: i32 = 8;

// Min length of V2 tunnelId.
pub const NEW_TUNNEL_ID_MIN_LENGTH: i32 = 3;

// Max length of V2 tunnelId.
pub const NEW_TUNNEL_ID_MAX_LENGTH: i32 = 60;

// Length of a tunnel alias.
pub const TUNNEL_ALIAS_LENGTH: i32 = 8;

// Min length of tunnel name.
pub const TUNNEL_NAME_MIN_LENGTH: i32 = 3;

// Max length of tunnel name.
pub const TUNNEL_NAME_MAX_LENGTH: i32 = 60;

// Max length of tunnel or port description.
pub const DESCRIPTION_MAX_LENGTH: i32 = 400;

// Max length of tunnel event details.
pub const EVENT_DETAILS_MAX_LENGTH: i32 = 4000;

// Max number of properties in a tunnel event.
pub const MAX_EVENT_PROPERTIES: i32 = 100;

// Max length of a single tunnel event property value.
pub const EVENT_PROPERTY_VALUE_MAX_LENGTH: i32 = 4000;

// Min length of a single tunnel or port tag.
pub const LABEL_MIN_LENGTH: i32 = 1;

// Max length of a single tunnel or port tag.
pub const LABEL_MAX_LENGTH: i32 = 50;

// Maximum number of labels that can be applied to a tunnel or port.
pub const MAX_LABELS: i32 = 100;

// Min length of a tunnel domain.
pub const TUNNEL_DOMAIN_MIN_LENGTH: i32 = 4;

// Max length of a tunnel domain.
pub const TUNNEL_DOMAIN_MAX_LENGTH: i32 = 180;

// Maximum number of items allowed in the tunnel ports array. The actual limit on number
// of ports that can be created may be much lower, and may depend on various resource
// limitations or policies.
pub const TUNNEL_MAX_PORTS: i32 = 1000;

// Maximum number of access control entries (ACEs) in a tunnel or tunnel port access
// control list (ACL).
pub const ACCESS_CONTROL_MAX_ENTRIES: i32 = 40;

// Maximum number of subjects (such as user IDs) in a tunnel or tunnel port access control
// entry (ACE).
pub const ACCESS_CONTROL_MAX_SUBJECTS: i32 = 100;

// Max length of an access control subject or organization ID.
pub const ACCESS_CONTROL_SUBJECT_MAX_LENGTH: i32 = 200;

// Max length of an access control subject name, when resolving names to IDs.
pub const ACCESS_CONTROL_SUBJECT_NAME_MAX_LENGTH: i32 = 200;

// Maximum number of scopes in an access control entry.
pub const ACCESS_CONTROL_MAX_SCOPES: i32 = 10;

// Regular expression that can match or validate tunnel event name strings.
pub const EVENT_NAME_PATTERN: &str = r#"^[a-z0-9_]{3,80}$"#;

// Regular expression that can match or validate tunnel event severity strings.
pub const EVENT_SEVERITY_PATTERN: &str = r#"^(info)|(warning)|(error)$"#;

// Regular expression that can match or validate tunnel event property name strings.
pub const EVENT_PROPERTY_NAME_PATTERN: &str = r#"^[a-zA-Z0-9_.]{3,200}$"#;

// Regular expression that can match or validate tunnel cluster ID strings.
//
// Cluster IDs are alphanumeric; hyphens are not permitted.
pub const CLUSTER_ID_PATTERN: &str = r#"^(([a-z]{3,4}[0-9]{1,3})|asse|aue|brs|euw|use)$"#;

// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
// excluding vowels and 'y' (to avoid accidentally generating any random words).
pub const OLD_TUNNEL_ID_CHARS: &str = r#"0123456789bcdfghjklmnpqrstvwxz"#;

// Regular expression that can match or validate tunnel ID strings.
//
// Tunnel IDs are fixed-length and have a limited character set of numbers and lowercase
// letters (minus vowels and y).
pub const OLD_TUNNEL_ID_PATTERN: &str = r#"[0123456789bcdfghjklmnpqrstvwxz]{8}"#;

// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
// excluding vowels and 'y' (to avoid accidentally generating any random words).
pub const NEW_TUNNEL_ID_CHARS: &str = r#"0123456789abcdefghijklmnopqrstuvwxyz-"#;

// Regular expression that can match or validate tunnel ID strings.
//
// Tunnel IDs are fixed-length and have a limited character set of numbers and lowercase
// letters (minus vowels and y).
pub const NEW_TUNNEL_ID_PATTERN: &str = r#"[a-z0-9][a-z0-9-]{1,58}[a-z0-9]"#;

// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
// excluding vowels and 'y' (to avoid accidentally generating any random words).
pub const TUNNEL_ALIAS_CHARS: &str = r#"0123456789bcdfghjklmnpqrstvwxz"#;

// Regular expression that can match or validate tunnel alias strings.
//
// Tunnel Aliases are fixed-length and have a limited character set of numbers and
// lowercase letters (minus vowels and y).
pub const TUNNEL_ALIAS_PATTERN: &str = r#"[0123456789bcdfghjklmnpqrstvwxz]{3,60}"#;

// Regular expression that can match or validate tunnel names.
//
// Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an empty
// string because tunnels may be unnamed.
pub const TUNNEL_NAME_PATTERN: &str = r#"([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|(^$)"#;

// Regular expression that can match or validate tunnel or port labels.
pub const LABEL_PATTERN: &str = r#"[\w-=]{1,50}"#;

// Regular expression that can match or validate tunnel domains.
//
// The tunnel service may perform additional contextual validation at the time the domain
// is registered.
pub const TUNNEL_DOMAIN_PATTERN: &str = r#"[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]|(^$)"#;

// Regular expression that can match or validate an access control subject or organization
// ID.
//
// The : and / characters are allowed because subjects may include IP addresses and
// ranges. The @ character is allowed because MSA subjects may be identified by email
// address.
pub const ACCESS_CONTROL_SUBJECT_PATTERN: &str = r#"[0-9a-zA-Z-._:/@]{0,200}"#;

// Regular expression that can match or validate an access control subject name, when
// resolving subject names to IDs.
//
// Note angle-brackets are only allowed when they wrap an email address as part of a
// formatted name with email. The service will block any other use of angle-brackets, to
// avoid any XSS risks.
pub const ACCESS_CONTROL_SUBJECT_NAME_PATTERN: &str = r#"[ \w\d-.,/'"_@()<>]{0,200}"#;
