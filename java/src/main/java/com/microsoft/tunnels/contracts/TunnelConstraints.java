// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelConstraints.cs

package com.microsoft.tunnels.contracts;

import java.util.regex.Pattern;

/**
 * Tunnel constraints.
 */
public class TunnelConstraints {
    /**
     * Min length of tunnel cluster ID.
     */
    public static final int clusterIdMinLength = 3;

    /**
     * Max length of tunnel cluster ID.
     */
    public static final int clusterIdMaxLength = 12;

    /**
     * Length of tunnel id.
     */
    public static final int tunnelIdLength = 8;

    /**
     * Min length of tunnel name.
     */
    public static final int tunnelNameMinLength = 3;

    /**
     * Max length of tunnel name.
     */
    public static final int tunnelNameMaxLength = 60;

    /**
     * Max length of tunnel or port description.
     */
    public static final int descriptionMaxLength = 400;

    /**
     * Min length of a single tunnel or port tag.
     */
    public static final int tagMinLength = 1;

    /**
     * Max length of a single tunnel or port tag.
     */
    public static final int tagMaxLength = 50;

    /**
     * Maximum number of tags that can be applied to a tunnel or port.
     */
    public static final int maxTags = 100;

    /**
     * Min length of a tunnel domain.
     */
    public static final int tunnelDomainMinLength = 4;

    /**
     * Max length of a tunnel domain.
     */
    public static final int tunnelDomainMaxLength = 180;

    /**
     * Maximum number of items allowed in the tunnel ports array. The actual limit on
     * number of ports that can be created may be much lower, and may depend on various
     * resource limitations or policies.
     */
    public static final int tunnelMaxPorts = 1000;

    /**
     * Maximum number of access control entries (ACEs) in a tunnel or tunnel port access
     * control list (ACL).
     */
    public static final int accessControlMaxEntries = 40;

    /**
     * Maximum number of subjects (such as user IDs) in a tunnel or tunnel port access
     * control entry (ACE).
     */
    public static final int accessControlMaxSubjects = 100;

    /**
     * Max length of an access control subject or organization ID.
     */
    public static final int accessControlSubjectMaxLength = 200;

    /**
     * Max length of an access control subject name, when resolving names to IDs.
     */
    public static final int accessControlSubjectNameMaxLength = 200;

    /**
     * Maximum number of scopes in an access control entry.
     */
    public static final int accessControlMaxScopes = 10;

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static final String clusterIdPattern = "[a-z][a-z0-9]{2,11}";

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static final Pattern clusterIdRegex = java.util.regex.Pattern.compile(TunnelConstraints.clusterIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    public static final String tunnelIdChars = "0123456789bcdfghjklmnpqrstvwxz";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final String tunnelIdPattern = "[" + TunnelConstraints.tunnelIdChars + "]{8}";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final Pattern tunnelIdRegex = java.util.regex.Pattern.compile(TunnelConstraints.tunnelIdPattern);

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    public static final String tunnelNamePattern = "([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|";

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    public static final Pattern tunnelNameRegex = java.util.regex.Pattern.compile(TunnelConstraints.tunnelNamePattern);

    /**
     * Regular expression that can match or validate tunnel or port tags.
     */
    public static final String tagPattern = "[\\w-=]{1,50}";

    /**
     * Regular expression that can match or validate tunnel or port tags.
     */
    public static final Pattern tagRegex = java.util.regex.Pattern.compile(TunnelConstraints.tagPattern);

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    public static final String tunnelDomainPattern = "[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]";

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    public static final Pattern tunnelDomainRegex = java.util.regex.Pattern.compile(TunnelConstraints.tunnelDomainPattern);

    /**
     * Regular expression that can match or validate an access control subject or
     * organization ID.
     *
     * The : and / characters are allowed because subjects may include IP addresses and
     * ranges.
     */
    public static final String accessControlSubjectPattern = "[0-9a-zA-Z-._:/]{0,200}";

    /**
     * Regular expression that can match or validate an access control subject or
     * organization ID.
     */
    public static final Pattern accessControlSubjectRegex = java.util.regex.Pattern.compile(TunnelConstraints.accessControlSubjectPattern);

    /**
     * Regular expression that can match or validate an access control subject name, when
     * resolving subject names to IDs.
     *
     * Note angle-brackets are only allowed when they wrap an email address as part of a
     * formatted name with email. The service will block any other use of angle-brackets,
     * to avoid any XSS risks.
     */
    public static final String accessControlSubjectNamePattern = "[ \\w\\d-.,'\"_@()<>]{0,200}";

    /**
     * Regular expression that can match or validate an access control subject name, when
     * resolving subject names to IDs.
     */
    public static final Pattern accessControlSubjectNameRegex = java.util.regex.Pattern.compile(TunnelConstraints.accessControlSubjectNamePattern);

    /**
     * Validates <paramref name="clusterId"/> and returns true if it is a valid cluster
     * ID, otherwise false.
     */
    public static boolean isValidClusterId(String clusterId) {
        return TunnelConstraintsStatics.isValidClusterId(clusterId);
    }

    /**
     * Validates <paramref name="tunnelId"/> and returns true if it is a valid tunnel id,
     * otherwise, false.
     */
    public static boolean isValidTunnelId(String tunnelId) {
        return TunnelConstraintsStatics.isValidTunnelId(tunnelId);
    }

    /**
     * Validates <paramref name="tunnelName"/> and returns true if it is a valid tunnel
     * name, otherwise, false.
     */
    public static boolean isValidTunnelName(String tunnelName) {
        return TunnelConstraintsStatics.isValidTunnelName(tunnelName);
    }

    /**
     * Validates <paramref name="tag"/> and returns true if it is a valid tunnel tag,
     * otherwise, false.
     */
    public static boolean isValidTag(String tag) {
        return TunnelConstraintsStatics.isValidTag(tag);
    }

    /**
     * Validates <paramref name="tunnelIdOrName"/> and returns true if it is a valid
     * tunnel id or name.
     */
    public static boolean isValidTunnelIdOrName(String tunnelIdOrName) {
        return TunnelConstraintsStatics.isValidTunnelIdOrName(tunnelIdOrName);
    }

    /**
     * Validates <paramref name="tunnelId"/> and throws exception if it is null or not a
     * valid tunnel id. Returns <paramref name="tunnelId"/> back if it's a valid tunnel
     * id.
     */
    public static String validateTunnelId(String tunnelId, String paramName) {
        return TunnelConstraintsStatics.validateTunnelId(tunnelId, paramName);
    }

    /**
     * Validates <paramref name="tunnelIdOrName"/> and throws exception if it is null or
     * not a valid tunnel id or name. Returns <paramref name="tunnelIdOrName"/> back if
     * it's a valid tunnel id.
     */
    public static String validateTunnelIdOrName(String tunnelIdOrName, String paramName) {
        return TunnelConstraintsStatics.validateTunnelIdOrName(tunnelIdOrName, paramName);
    }
}
