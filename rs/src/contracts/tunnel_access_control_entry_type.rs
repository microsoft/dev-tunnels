// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntryType.cs

use serde::{Deserialize, Serialize};
use std::fmt;

// Specifies the type of `TunnelAccessControlEntry`.
#[derive(Clone, Debug, Deserialize, Serialize)]
pub enum TunnelAccessControlEntryType {
    // Uninitialized access control entry type.
    None,

    // The access control entry refers to all anonymous users.
    Anonymous,

    // The access control entry is a list of user IDs that are allowed (or denied) access.
    Users,

    // The access control entry is a list of groups IDs that are allowed (or denied)
    // access.
    Groups,

    // The access control entry is a list of organization IDs that are allowed (or denied)
    // access.
    //
    // All users in the organizations are allowed (or denied) access, unless overridden by
    // following group or user rules.
    Organizations,

    // The access control entry is a list of repositories. Users are allowed access to the
    // tunnel if they have access to the repo.
    Repositories,

    // The access control entry is a list of public keys. Users are allowed access if they
    // can authenticate using a private key corresponding to one of the public keys.
    PublicKeys,

    // The access control entry is a list of IP address ranges that are allowed (or
    // denied) access to the tunnel. Ranges can be IPv4, IPv6, or Azure service tags.
    IPAddressRanges,
}

impl fmt::Display for TunnelAccessControlEntryType {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            TunnelAccessControlEntryType::None => write!(f, "None"),
            TunnelAccessControlEntryType::Anonymous => write!(f, "Anonymous"),
            TunnelAccessControlEntryType::Users => write!(f, "Users"),
            TunnelAccessControlEntryType::Groups => write!(f, "Groups"),
            TunnelAccessControlEntryType::Organizations => write!(f, "Organizations"),
            TunnelAccessControlEntryType::Repositories => write!(f, "Repositories"),
            TunnelAccessControlEntryType::PublicKeys => write!(f, "PublicKeys"),
            TunnelAccessControlEntryType::IPAddressRanges => write!(f, "IPAddressRanges"),
        }
    }
}
