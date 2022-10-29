// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConstraints.cs
/* eslint-disable */

/**
 * Tunnel constraints.
 */
namespace TunnelConstraints {
    /**
     * Min length of tunnel cluster ID.
     */
    export const clusterIdMinLength: number = 3;

    /**
     * Max length of tunnel cluster ID.
     */
    export const clusterIdMaxLength: number = 12;

    /**
     * Length of tunnel id.
     */
    export const tunnelIdLength: number = 8;

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
    export const tagMinLength: number = 1;

    /**
     * Max length of a single tunnel or port tag.
     */
    export const tagMaxLength: number = 50;

    /**
     * Maximum number of tags that can be applied to a tunnel or port.
     */
    export const maxTags: number = 100;

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
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    export const clusterIdPattern: string = '[a-z][a-z0-9]{2,11}';

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
    export const tunnelIdChars: string = '0123456789bcdfghjklmnpqrstvwxz';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const tunnelIdPattern: string = '[' + TunnelConstraints.tunnelIdChars + ']{8}';

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    export const tunnelIdRegex: RegExp = new RegExp(TunnelConstraints.tunnelIdPattern);

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    export const tunnelNamePattern: string = '([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|';

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    export const tunnelNameRegex: RegExp = new RegExp(TunnelConstraints.tunnelNamePattern);

    /**
     * Regular expression that can match or validate tunnel or port tags.
     */
    export const tagPattern: string = '[\\w-=]{1,50}';

    /**
     * Regular expression that can match or validate tunnel or port tags.
     */
    export const tagRegex: RegExp = new RegExp(TunnelConstraints.tagPattern);

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    export const tunnelDomainPattern: string = '[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]';

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    export const tunnelDomainRegex: RegExp = new RegExp(TunnelConstraints.tunnelDomainPattern);
}
