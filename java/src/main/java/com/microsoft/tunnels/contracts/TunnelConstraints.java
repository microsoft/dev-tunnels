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
    public static int clusterIdMinLength = 3;

    /**
     * Max length of tunnel cluster ID.
     */
    public static int clusterIdMaxLength = 12;

    /**
     * Characters that are valid in tunnel id. Vowels and 'y' are excluded to avoid
     * accidentally generating any random words.
     */
    public static String tunnelIdChars = "0123456789bcdfghjklmnpqrstvwxz";

    /**
     * Length of tunnel id.
     */
    public static int tunnelIdLength = 8;

    /**
     * Min length of tunnel name.
     */
    public static int tunnelNameMinLength = 3;

    /**
     * Max length of tunnel name.
     */
    public static int tunnelNameMaxLength = 60;

    /**
     * Gets a regular expression that can match or validate tunnel cluster ID strings.
     *
     * Cluster IDs are alphanumeric; hyphens are not permitted.
     */
    public static Pattern clusterIdRegex = java.util.regex.Pattern.compile(
        "[a-z][a-z0-9]{" + (clusterIdMinLength - 1) + "," + (clusterIdMaxLength - 1) + "}");

    /**
     * Gets a regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and some
     * lowercase letters (minus vowels).
     */
    public static Pattern tunnelIdRegex = java.util.regex.Pattern.compile(
        "[" + tunnelIdChars.replace("0123456789", "0-9") + "]{" + tunnelIdLength + "}");

    /**
     * Gets a regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens.
     */
    public static Pattern tunnelNameRegex = java.util.regex.Pattern.compile(
        "[a-z0-9][a-z0-9-]{" +
        (tunnelNameMinLength - 2) + "," + (tunnelNameMaxLength - 2) +
        "}[a-z0-9]");

    /**
     * Gets a regular expression that can match or validate tunnel names.
     */
    public static Pattern tunnelTagRegex = java.util.regex.Pattern.compile("^[\\w-=]+$");

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
    public static boolean isValidTunnelTag(String tag) {
        return TunnelConstraintsStatics.isValidTunnelTag(tag);
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
