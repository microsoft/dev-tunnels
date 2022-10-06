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
     * Characters that are valid in tunnel id. Vowels and 'y' are excluded to avoid
     * accidentally generating any random words.
     */
    export const tunnelIdChars: string = '0123456789bcdfghjklmnpqrstvwxz';

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
     * Gets a regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    export const clusterIdRegex: RegExp = new RegExp(
        '[a-z][a-z0-9]{' + (clusterIdMinLength - 1) + ',' + (clusterIdMaxLength - 1) + '}');

    /**
     * Gets a regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and some
     * lowercase letters (minus vowels).
     */
    export const tunnelIdRegex: RegExp = new RegExp(
        '[' + tunnelIdChars.replace('0123456789', '0-9') + ']{' + tunnelIdLength + '}');

    /**
     * Gets a regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens.
     */
    export const tunnelNameRegex: RegExp = new RegExp(
        '[a-z0-9][a-z0-9-]{' +
        (tunnelNameMinLength - 2) + ',' + (tunnelNameMaxLength - 2) +
        '}[a-z0-9]');

    /**
     * Gets a regular expression that can match or validate tunnel names.
     */
    export const tunnelTagRegex: RegExp = new RegExp('^[\\w-=]+$');
}
