// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelAccessControlEntryType.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

/**
 * Specifies the type of {@link TunnelAccessControlEntry}.
 */
public enum TunnelAccessControlEntryType {
    /**
     * Uninitialized access control entry type.
     */
    @SerializedName("None")
    None,

    /**
     * The access control entry refers to all anonymous users.
     */
    @SerializedName("Anonymous")
    Anonymous,

    /**
     * The access control entry is a list of user IDs that are allowed (or denied) access.
     */
    @SerializedName("Users")
    Users,

    /**
     * The access control entry is a list of groups IDs that are allowed (or denied)
     * access.
     */
    @SerializedName("Groups")
    Groups,

    /**
     * The access control entry is a list of organization IDs that are allowed (or denied)
     * access.
     *
     * All users in the organizations are allowed (or denied) access, unless overridden by
     * following group or user rules.
     */
    @SerializedName("Organizations")
    Organizations,

    /**
     * The access control entry is a list of repositories. Users are allowed access to the
     * tunnel if they have access to the repo.
     */
    @SerializedName("Repositories")
    Repositories,

    /**
     * The access control entry is a list of public keys. Users are allowed access if they
     * can authenticate using a private key corresponding to one of the public keys.
     */
    @SerializedName("PublicKeys")
    PublicKeys,

    /**
     * The access control entry is a list of IP address ranges that are allowed (or
     * denied) access to the tunnel.
     */
    @SerializedName("IPAddressRanges")
    IPAddressRanges,

    /**
     * The access control entry is a list of service tags representing Azure services that
     * are allowed or denied access to the tunnel. These are resolve to IP address ranges
     * by the tunnel service.
     */
    @SerializedName("ServiceTags")
    ServiceTags,
}
