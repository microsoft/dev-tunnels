// Generated from ../../../../../../../../cs/src/Contracts/TunnelConstraints.cs

package com.microsoft.tunnels.contracts;

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
    public static java.util.regex.Pattern clusterIdRegex = java.util.regex.Pattern.compile(
        "[a-z][a-z0-9]{" + (clusterIdMinLength - 1) + "," + (clusterIdMaxLength - 1) + "}");

    /**
     * Gets a regular expression that can match or validate tunnel ID strings.
     *
     * Tunnel IDs are fixed-length and have a limited character set of numbers and some
     * lowercase letters (minus vowels).
     */
    public static java.util.regex.Pattern tunnelIdRegex = java.util.regex.Pattern.compile(
        "[" + tunnelIdChars.replace("0123456789", "0-9") + "]{" + tunnelIdLength + "}");

    /**
     * Gets a regular expression that can match or validate tunnel names.
     *
     * Tunnel names are alphanumeric and may contain hyphens.
     */
    public static java.util.regex.Pattern tunnelNameRegex = java.util.regex.Pattern.compile(
        "[a-z0-9][a-z0-9-]{" +
        (tunnelNameMinLength - 2) + "," + (tunnelNameMaxLength - 2) +
        "}[a-z0-9]");

    /**
     * Gets a regular expression that can match or validate tunnel names.
     */
    public static java.util.regex.Pattern tunnelTagRegex = java.util.regex.Pattern.compile("^[\\w-=]+$");
}
