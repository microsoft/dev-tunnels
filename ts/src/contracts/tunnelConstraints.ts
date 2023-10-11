// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConstraints.cs
/* eslint-disable */

/**
 * Tunnel constraints.
 */
export namespace TunnelConstraints {
    /**
     * Min length of tunnel cluster ID.
     */
    export const clusterIdMinLength: number = 3;

    /**
     * Max length of tunnel cluster ID.
     */
    export const clusterIdMaxLength: number = 12;

    /**
     * Length of V1 tunnel id.
     */
    export const oldTunnelIdLength: number = 8;

    /**
     * Min length of V2 tunnelId.
     */
    export const newTunnelIdMinLength: number = 3;

    /**
     * Max length of V2 tunnelId.
     */
    export const newTunnelIdMaxLength: number = 60;

    /**
     * Length of a tunnel alias.
     */
    export const tunnelAliasLength: number = 8;

    /**
     * Min length of tunnel name.
     */
    export const tunnelNameMinLength: number = 3;

    /**
     * Max length of tunnel name.
     */
    export const tunnelNameMaxLength: number = 60;

    /**
     * Max length of tunnel or port description.
     */
    export const descriptionMaxLength: number = 400;

    /**
     * Min length of a single tunnel or port tag.
     */
    export const labelMinLength: number = 1;

    /**
     * Max length of a single tunnel or port tag.
     */
    export const labelMaxLength: number = 50;

    /**
     * Maximum number of labels that can be applied to a tunnel or port.
     */
    export const maxLabels: number = 100;

    /**
     * Min length of a tunnel domain.
     */
    export const tunnelDomainMinLength: number = 4;

    /**
     * Max length of a tunnel domain.
     */
    export const tunnelDomainMaxLength: number = 180;

    /**
     * Maximum number of items allowed in the tunnel ports array. The actual limit on
     * number of ports that can be created may be much lower, and may depend on various
     * resource limitations or policies.
     */
    export const tunnelMaxPorts: number = 1000;

    /**
     * Maximum number of access control entries (ACEs) in a tunnel or tunnel port access
     * control list (ACL).
     */
    export const accessControlMaxEntries: number = 40;

    /**
     * Maximum number of subjects (such as user IDs) in a tunnel or tunnel port access
     * control entry (ACE).
     */
    export const accessControlMaxSubjects: number = 100;

    /**
     * Max length of an access control subject or organization ID.
     */
    export const accessControlSubjectMaxLength: number = 200;

    /**
     * Max length of an access control subject name, when resolving names to IDs.
     */
    export const accessControlSubjectNameMaxLength: number = 200;

    /**
     * Maximum number of scopes in an access control entry.
     */
    export const accessControlMaxScopes: number = 10;

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    export const clusterIdPattern: string = '^(([a-z]{3,4}[0-9]{1,3})|asse|aue|brs|euw|use)$';

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    export const clusterIdRegex: RegExp = new RegExp(TunnelConstraints.clusterIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    export const oldTunnelIdChars: string = '0123456789bcdfghjklmnpqrstvwxz';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const oldTunnelIdPattern: string = '[' + TunnelConstraints.oldTunnelIdChars + ']{8}';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const oldTunnelIdRegex: RegExp = new RegExp(TunnelConstraints.oldTunnelIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    export const newTunnelIdChars: string = '0123456789abcdefghijklmnopqrstuvwxyz-';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const newTunnelIdPattern: string = '[a-z0-9][a-z0-9-]{1,58}[a-z0-9]';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const newTunnelIdRegex: RegExp = new RegExp(TunnelConstraints.newTunnelIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    export const tunnelAliasChars: string = '0123456789bcdfghjklmnpqrstvwxz';

    /**
     * Regular expression that can match or validate tunnel alias strings.
     *
     * Tunnel Aliases are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const tunnelAliasPattern: string = '[' + TunnelConstraints.tunnelAliasChars + ']{3,60}';

    /**
     * Regular expression that can match or validate tunnel alias strings.
     *
     * Tunnel Aliases are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const tunnelAliasRegex: RegExp = new RegExp(TunnelConstraints.tunnelAliasPattern);

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    export const tunnelNamePattern: string = '([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|(^$)';

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    export const tunnelNameRegex: RegExp = new RegExp(TunnelConstraints.tunnelNamePattern);

    /**
     * Regular expression that can match or validate tunnel or port labels.
     */
    export const labelPattern: string = '[\\w-=]{1,50}';

    /**
     * Regular expression that can match or validate tunnel or port labels.
     */
    export const labelRegex: RegExp = new RegExp(TunnelConstraints.labelPattern);

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    export const tunnelDomainPattern: string = '[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]|(^$)';

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    export const tunnelDomainRegex: RegExp = new RegExp(TunnelConstraints.tunnelDomainPattern);

    /**
     * Regular expression that can match or validate an access control subject or
     * organization ID.
     *
     * The : and / characters are allowed because subjects may include IP addresses and
     * ranges. The @ character is allowed because MSA subjects may be identified by email
     * address.
     */
    export const accessControlSubjectPattern: string = '[0-9a-zA-Z-._:/@]{0,200}';

    /**
     * Regular expression that can match or validate an access control subject or
     * organization ID.
     */
    export const accessControlSubjectRegex: RegExp = new RegExp(TunnelConstraints.accessControlSubjectPattern);

    /**
     * Regular expression that can match or validate an access control subject name, when
     * resolving subject names to IDs.
     *
     * Note angle-brackets are only allowed when they wrap an email address as part of a
     * formatted name with email. The service will block any other use of angle-brackets,
     * to avoid any XSS risks.
     */
    export const accessControlSubjectNamePattern: string = '[ \\w\\d-.,/\'"_@()<>]{0,200}';

    /**
     * Regular expression that can match or validate an access control subject name, when
     * resolving subject names to IDs.
     */
    export const accessControlSubjectNameRegex: RegExp = new RegExp(TunnelConstraints.accessControlSubjectNamePattern);
}
