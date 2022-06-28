// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntryType.cs
/* eslint-disable */

/**
 * Specifies the type of {@link TunnelAccessControlEntry}.
 */
export enum TunnelAccessControlEntryType {
    /**
     * Uninitialized access control entry type.
     */
    None = 'None',

    /**
     * The access control entry refers to all anonymous users.
     */
    Anonymous = 'Anonymous',

    /**
     * The access control entry is a list of user IDs that are allowed (or denied) access.
     */
    Users = 'Users',

    /**
     * The access control entry is a list of groups IDs that are allowed (or denied)
     * access.
     */
    Groups = 'Groups',

    /**
     * The access control entry is a list of organization IDs that are allowed (or denied)
     * access.
     *
     * All users in the organizations are allowed (or denied) access, unless overridden by
     * following group or user rules.
     */
    Organizations = 'Organizations',

    /**
     * The access control entry is a list of repositories. Users are allowed access to the
     * tunnel if they have access to the repo.
     */
    Repositories = 'Repositories',

    /**
     * The access control entry is a list of public keys. Users are allowed access if they
     * can authenticate using a private key corresponding to one of the public keys.
     */
    PublicKeys = 'PublicKeys',

    /**
     * The access control entry is a list of IP address ranges that are allowed (or
     * denied) access to the tunnel. Ranges can be IPv4, IPv6, or Azure service tags.
     */
    IPAddressRanges = 'IPAddressRanges',
}
