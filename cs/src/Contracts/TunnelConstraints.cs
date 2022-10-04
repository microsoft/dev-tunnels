// <copyright file="TunnelConstraints.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text.RegularExpressions;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Tunnel constraints.
/// </summary>
public static class TunnelConstraints
{
    /// <summary>
    /// Min length of tunnel cluster ID.
    /// </summary>
    public static int ClusterIdMinLength { get; } = 3;

    /// <summary>
    /// Max length of tunnel cluster ID.
    /// </summary>
    public static int ClusterIdMaxLength { get; } = 12;

    /// <summary>
    /// Characters that are valid in tunnel id. Vowels and 'y' are excluded
    /// to avoid accidentally generating any random words.
    /// </summary>
    public static string TunnelIdChars { get; } = "0123456789bcdfghjklmnpqrstvwxz";

    /// <summary>
    /// Length of tunnel id.
    /// </summary>
    public static int TunnelIdLength { get; } = 8;

    /// <summary>
    /// Min length of tunnel name.
    /// </summary>
    public static int TunnelNameMinLength { get; } = 3;

    /// <summary>
    /// Max length of tunnel name.
    /// </summary>
    public static int TunnelNameMaxLength { get; } = 60;

    /// <summary>
    /// Gets a regular expression that can match or validate tunnel cluster ID strings.
    /// </summary>
    /// <remarks>
    /// Cluster IDs are alphanumeric; hyphens are not permitted.
    /// </remarks>
    public static Regex ClusterIdRegex { get; } = new Regex(
        "[a-z][a-z0-9]{" + (ClusterIdMinLength - 1) + "," + (ClusterIdMaxLength - 1) + "}");

    /// <summary>
    /// Gets a regular expression that can match or validate tunnel ID strings.
    /// </summary>
    /// <remarks>
    /// Tunnel IDs are fixed-length and have a limited character set of
    /// numbers and some lowercase letters (minus vowels).
    /// </remarks>
    public static Regex TunnelIdRegex { get; } = new Regex(
        "[" + TunnelIdChars.Replace("0123456789", "0-9") + "]{" + TunnelIdLength + "}");

    /// <summary>
    /// Gets a regular expression that can match or validate tunnel names.
    /// </summary>
    /// <remarks>
    /// Tunnel names are alphanumeric and may contain hyphens.
    /// </remarks>
    public static Regex TunnelNameRegex { get; } = new Regex(
        "[a-z0-9][a-z0-9-]{" +
        (TunnelNameMinLength - 2) + "," + (TunnelNameMaxLength - 2) +
        "}[a-z0-9]");

    /// <summary>
    /// Gets a regular expression that can match or validate tunnel names.
    /// </summary>
    public static Regex TunnelTagRegex { get; } = new Regex("^[\\w-=]+$");

    /// <summary>
    /// Validates <paramref name="clusterId"/> and returns true if it is a valid cluster ID, otherwise false.
    /// </summary>
    public static bool IsValidClusterId(string clusterId)
    {
        return Equals(ClusterIdRegex.Match(clusterId).Value, clusterId);
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and returns true if it is a valid tunnel id, otherwise, false.
    /// </summary>
    public static bool IsValidTunnelId(string tunnelId)
    {
        return tunnelId?.Length == TunnelIdLength &&
            TunnelIdRegex.IsMatch(tunnelId);
    }

    /// <summary>
    /// Validates <paramref name="tunnelName"/> and returns true if it is a valid tunnel name, otherwise, false.
    /// </summary>
    public static bool IsValidTunnelName(string tunnelName)
    {
        return Equals(TunnelNameRegex.Match(tunnelName).Value, tunnelName) &&
            !IsValidTunnelId(tunnelName);
    }

    /// <summary>
    /// Validates <paramref name="tag"/> and returns true if it is a valid tunnel tag, otherwise, false.
    /// </summary>
    public static bool IsValidTunnelTag(string tag)
    {
        return TunnelTagRegex.IsMatch(tag);
    }
    
    /// <summary>
    /// Validates <paramref name="tunnelIdOrName"/> and returns true if it is a valid tunnel id or name.
    /// </summary>
    public static bool IsValidTunnelIdOrName(string tunnelIdOrName)
    {
        return tunnelIdOrName != null &&
            TunnelNameRegex.IsMatch(tunnelIdOrName); // Tunnel ID Regex is a subset of Tunnel name Regex
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and throws exception if it is null or not a valid tunnel id.
    /// Returns <paramref name="tunnelId"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelId"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelId"/> is not a valid tunnel id.</exception>
    public static string ValidateTunnelId(string tunnelId, string? paramName = default)
    {
        paramName ??= nameof(tunnelId);

        if (tunnelId == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (!IsValidTunnelId(tunnelId))
        {
            throw new ArgumentException("Invalid tunnel id", paramName);
        }

        return tunnelId;
    }

    /// <summary>
    /// Validates <paramref name="tunnelIdOrName"/> and throws exception if it is null or not a valid tunnel id or name.
    /// Returns <paramref name="tunnelIdOrName"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelIdOrName"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelIdOrName"/> is not a valid tunnel id or name.</exception>
    public static string ValidateTunnelIdOrName(string tunnelIdOrName, string? paramName = default)
    {
        paramName ??= nameof(tunnelIdOrName);

        if (tunnelIdOrName == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (!IsValidTunnelIdOrName(tunnelIdOrName))
        {
            throw new ArgumentException("Invalid tunnel id or name", paramName);
        }

        return tunnelIdOrName;
    }
}
