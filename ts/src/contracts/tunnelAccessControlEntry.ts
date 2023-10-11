// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAccessControlEntry.cs
/* eslint-disable */

import { TunnelAccessControlEntryType } from './tunnelAccessControlEntryType';

/**
 * Data contract for an access control entry on a {@link Tunnel} or {@link TunnelPort}.
 *
 * An access control entry (ACE) grants or denies one or more access scopes to one or more
 * subjects. Tunnel ports inherit access control entries from their tunnel, and they may
 * have additional port-specific entries that augment or override those access rules.
 */
export interface TunnelAccessControlEntry {
    /**
     * Gets or sets the access control entry type.
     */
    type: TunnelAccessControlEntryType;

    /**
     * Gets or sets the provider of the subjects in this access control entry. The
     * provider impacts how the subject identifiers are resolved and displayed. The
     * provider may be an identity provider such as AAD, or a system or standard such as
     * "ssh" or "ipv4".
     *
     * For user, group, or org ACEs, this value is the name of the identity provider of
     * the user/group/org IDs. It may be one of the well-known provider names in {@link
     * TunnelAccessControlEntry.providers}, or (in the future) a custom identity provider.
     *  For public key ACEs, this value is the type of public key, e.g. "ssh".  For IP
     * address range ACEs, this value is the IP address version, "ipv4" or "ipv6", or
     * "service-tag" if the range is defined by an Azure service tag.  For anonymous ACEs,
     * this value is null.
     */
    provider?: string;

    /**
     * Gets or sets a value indicating whether this is an access control entry on a tunnel
     * port that is inherited from the tunnel's access control list.
     */
    isInherited?: boolean;

    /**
     * Gets or sets a value indicating whether this entry is a deny rule that blocks
     * access to the specified users. Otherwise it is an allow rule.
     *
     * All deny rules (including inherited rules) are processed after all allow rules.
     * Therefore a deny ACE cannot be overridden by an allow ACE that is later in the list
     * or on a more-specific resource. In other words, inherited deny ACEs cannot be
     * overridden.
     */
    isDeny?: boolean;

    /**
     * Gets or sets a value indicating whether this entry applies to all subjects that are
     * NOT in the {@link TunnelAccessControlEntry.subjects} list.
     *
     * Examples: an inverse organizations ACE applies to all users who are not members of
     * the listed organization(s); an inverse anonymous ACE applies to all authenticated
     * users; an inverse IP address ranges ACE applies to all clients that are not within
     * any of the listed IP address ranges. The inverse option is often useful in policies
     * in combination with {@link TunnelAccessControlEntry.isDeny}, for example a policy
     * could deny access to users who are not members of an organization or are outside of
     * an IP address range, effectively blocking any tunnels from allowing outside access
     * (because inherited deny ACEs cannot be overridden).
     */
    isInverse?: boolean;

    /**
     * Gets or sets an optional organization context for all subjects of this entry. The
     * use and meaning of this value depends on the {@link TunnelAccessControlEntry.type}
     * and {@link TunnelAccessControlEntry.provider} of this entry.
     *
     * For AAD users and group ACEs, this value is the AAD tenant ID. It is not currently
     * used with any other types of ACEs.
     */
    organization?: string;

    /**
     * Gets or sets the subjects for the entry, such as user or group IDs. The format of
     * the values depends on the {@link TunnelAccessControlEntry.type} and {@link
     * TunnelAccessControlEntry.provider} of this entry.
     */
    subjects: string[];

    /**
     * Gets or sets the access scopes that this entry grants or denies to the subjects.
     *
     * These must be one or more values from {@link TunnelAccessScopes}.
     */
    scopes: string[];

    /**
     * Gets or sets the expiration for an access control entry.
     *
     * If no value is set then this value is null.
     */
    expiration?: Date;
}

export namespace TunnelAccessControlEntry {
    /**
     * Constants for well-known identity providers.
     */
    export enum Providers {
        /**
         * Microsoft (AAD) identity provider.
         */
        Microsoft = 'microsoft',

        /**
         * GitHub identity provider.
         */
        GitHub = 'github',

        /**
         * SSH public keys.
         */
        Ssh = 'ssh',

        /**
         * IPv4 addresses.
         */
        IPv4 = 'ipv4',

        /**
         * IPv6 addresses.
         */
        IPv6 = 'ipv6',

        /**
         * Service tags.
         */
        ServiceTag = 'service-tag',
    }
}
