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
     * Length of V1 tunnel id.
     */
    public static final int oldTunnelIdLength = 8;

    /**
     * Min length of V2 tunnelId.
     */
    public static final int newTunnelIdMinLength = 3;

    /**
     * Max length of V2 tunnelId.
     */
    public static final int newTunnelIdMaxLength = 60;

    /**
     * Length of a tunnel alias.
     */
    public static final int tunnelAliasLength = 8;

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
     * Max length of tunnel event details.
     */
    public static final int eventDetailsMaxLength = 4000;

    /**
     * Max number of properties in a tunnel event.
     */
    public static final int maxEventProperties = 100;

    /**
     * Max length of a single tunnel event property value.
     */
    public static final int eventPropertyValueMaxLength = 4000;

    /**
     * Min length of a single tunnel or port tag.
     */
    public static final int labelMinLength = 1;

    /**
     * Max length of a single tunnel or port tag.
     */
    public static final int labelMaxLength = 50;

    /**
     * Maximum number of labels that can be applied to a tunnel or port.
     */
    public static final int maxLabels = 100;

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
     * Regular expression that can match or validate tunnel event name strings.
     */
    public static final String eventNamePattern = "^[a-z0-9_]{3,80}$";

    /**
     * Regular expression that can match or validate tunnel event severity strings.
     */
    public static final String eventSeverityPattern = "^(info)|(warning)|(error)$";

    /**
     * Regular expression that can match or validate tunnel event property name strings.
     */
    public static final String eventPropertyNamePattern = "^[a-zA-Z0-9_.]{3,200}$";

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static final String clusterIdPattern = "^(([a-z]{3,4}[0-9]{1,3})|asse|aue|brs|euw|use)$";

    /**
     * Regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static final Pattern clusterIdRegex = java.util.regex.Pattern.compile(TunnelConstraints.clusterIdPattern);

    /**
     * Regular expression that can match or validate a tunnel cluster ID as a hostname
     * prefix.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static final Pattern clusterIdPrefixRegex = java.util.regex.Pattern.compile(TunnelConstraints.clusterIdPattern.replace("$", "\\."));

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    public static final String oldTunnelIdChars = "0123456789bcdfghjklmnpqrstvwxz";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final String oldTunnelIdPattern = "[" + TunnelConstraints.oldTunnelIdChars + "]{8}";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final Pattern oldTunnelIdRegex = java.util.regex.Pattern.compile(TunnelConstraints.oldTunnelIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    public static final String newTunnelIdChars = "0123456789abcdefghijklmnopqrstuvwxyz-";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final String newTunnelIdPattern = "[a-z0-9][a-z0-9-]{1,58}[a-z0-9]";

    /**
     * Regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final Pattern newTunnelIdRegex = java.util.regex.Pattern.compile(TunnelConstraints.newTunnelIdPattern);

    /**
     * Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
     * excluding vowels and 'y' (to avoid accidentally generating any random words).
     */
    public static final String tunnelAliasChars = "0123456789bcdfghjklmnpqrstvwxz";

    /**
     * Regular expression that can match or validate tunnel alias strings.
     *
     * Tunnel Aliases are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final String tunnelAliasPattern = "[" + TunnelConstraints.tunnelAliasChars + "]{3,60}";

    /**
     * Regular expression that can match or validate tunnel alias strings.
     *
     * Tunnel Aliases are fixed-length and have a limited character set of numbers and
     * lowercase letters (minus vowels and y).
     */
    public static final Pattern tunnelAliasRegex = java.util.regex.Pattern.compile(TunnelConstraints.tunnelAliasPattern);

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    public static final String tunnelNamePattern = "([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|(^$)";

    /**
     * Regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens. The pattern also allows an
     * empty string because tunnels may be unnamed.
     */
    public static final Pattern tunnelNameRegex = java.util.regex.Pattern.compile(TunnelConstraints.tunnelNamePattern);

    /**
     * Regular expression that can match or validate tunnel or port labels.
     */
    public static final String labelPattern = "[\\w-=]{1,50}";

    /**
     * Regular expression that can match or validate tunnel or port labels.
     */
    public static final Pattern labelRegex = java.util.regex.Pattern.compile(TunnelConstraints.labelPattern);

    /**
     * Regular expression that can match or validate tunnel domains.
     *
     * The tunnel service may perform additional contextual validation at the time the
     * domain is registered.
     */
    public static final String tunnelDomainPattern = "[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]|(^$)";

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
     * ranges. The @ character is allowed because MSA subjects may be identified by email
     * address.
     */
    public static final String accessControlSubjectPattern = "[0-9a-zA-Z-._:/@]{0,200}";

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
    public static final String accessControlSubjectNamePattern = "[ \\w\\d-.,/'\"_@()<>]{0,200}";

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
    public static boolean isValidOldTunnelId(String tunnelId) {
        return TunnelConstraintsStatics.isValidOldTunnelId(tunnelId);
    }

    /**
     * Validates <paramref name="tunnelId"/> and returns true if it is a valid tunnel id,
     * otherwise, false.
     */
    public static boolean isValidNewTunnelId(String tunnelId) {
        return TunnelConstraintsStatics.isValidNewTunnelId(tunnelId);
    }

    /**
     * Validates <paramref name="alias"/> and returns true if it is a valid tunnel alias,
     * otherwise, false.
     */
    public static boolean isValidTunnelAlias(String alias) {
        return TunnelConstraintsStatics.isValidTunnelAlias(alias);
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
    public static String validateOldTunnelId(String tunnelId, String paramName) {
        return TunnelConstraintsStatics.validateOldTunnelId(tunnelId, paramName);
    }

    /**
     * Validates <paramref name="tunnelId"/> and throws exception if it is null or not a
     * valid tunnel id. Returns <paramref name="tunnelId"/> back if it's a valid tunnel
     * id.
     */
    public static String validateNewOrOldTunnelId(String tunnelId, String paramName) {
        return TunnelConstraintsStatics.validateNewOrOldTunnelId(tunnelId, paramName);
    }

    /**
     * Validates <paramref name="tunnelId"/> and throws exception if it is null or not a
     * valid tunnel id. Returns <paramref name="tunnelId"/> back if it's a valid tunnel
     * id.
     */
    public static String validateNewTunnelId(String tunnelId, String paramName) {
        return TunnelConstraintsStatics.validateNewTunnelId(tunnelId, paramName);
    }

    /**
     * Validates <paramref name="tunnelAlias"/> and throws exception if it is null or not
     * a valid tunnel id. Returns <paramref name="tunnelAlias"/> back if it's a valid
     * tunnel id.
     */
    public static String validateTunnelAlias(String tunnelAlias, String paramName) {
        return TunnelConstraintsStatics.validateTunnelAlias(tunnelAlias, paramName);
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
