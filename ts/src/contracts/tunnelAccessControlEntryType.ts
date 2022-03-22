// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntryType.cs

/**
 * Specifies the type of {@link TunnelAccessControlEntry}.
 */
export enum TunnelAccessControlEntryType {
    /**
     * Uninitialized access control entry type.
     */
    None = 'none',

    /**
     * The access control entry refers to all anonymous users.
     */
    Anonymous = 'anonymous',

    /**
     * The access control entry is a list of user IDs that are allowed (or denied) access.
     */
    Users = 'users',

    /**
     * The access control entry is a list of groups IDs that are allowed (or denied)
     * access.
     */
    Groups = 'groups',

    /**
     * The access control entry is a list of organization IDs that are allowed (or denied)
     * access.
     *
     * All users in the organizations are allowed (or denied) access, unless overridden by
     * following group or user rules.
     */
    Organizations = 'organizations',

    /**
     * The access control entry is a list of repositories. Users are allowed access to the
     * tunnel if they have access to the repo.
     */
    Repositories = 'repositories',

    /**
     * The access control entry is a list of public keys. Users are allowed access if they
     * can authenticate using a private key corresponding to one of the public keys.
     */
    PublicKeys = 'publicKeys',

    /**
     * The access control entry is a list of IP address ranges that are allowed (or
     * denied) access to the tunnel.
     */
    IPAddressRanges = 'iPAddressRanges',
}
