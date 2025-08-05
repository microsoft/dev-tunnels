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
    // Note regular expression patterns must be string constants (for use in attributes), so they
    // cannot reference the corresponding min/max length integer constants. Be sure to also update
    // the regex patterns updating min/max length integer constants.

    /// <summary>
    /// Min length of tunnel cluster ID.
    /// </summary>
    /// <seealso cref="Tunnel.ClusterId"/>
    public const int ClusterIdMinLength = 3;

    /// <summary>
    /// Max length of tunnel cluster ID.
    /// </summary>
    /// <seealso cref="Tunnel.ClusterId"/>
    public const int ClusterIdMaxLength = 12;

    /// <summary>
    /// Length of V1 tunnel id.
    /// </summary>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const int OldTunnelIdLength = 8;

    /// <summary>
    /// Min length of V2 tunnelId.
    /// </summary>
    public const int NewTunnelIdMinLength = 3;

    /// <summary>
    /// Max length of V2 tunnelId.
    /// </summary>
    public const int NewTunnelIdMaxLength = 60;

    /// <summary>
    /// Length of a tunnel alias.
    /// </summary>
    public const int TunnelAliasLength = 8;

    /// <summary>
    /// Min length of tunnel name.
    /// </summary>
    /// <seealso cref="Tunnel.Name"/>
    public const int TunnelNameMinLength = 3;

    /// <summary>
    /// Max length of tunnel name.
    /// </summary>
    /// <seealso cref="Tunnel.Name"/>
    public const int TunnelNameMaxLength = 60;

    /// <summary>
    /// Max length of tunnel or port description.
    /// </summary>
    /// <seealso cref="Tunnel.Description"/>
    /// <seealso cref="TunnelPort.Description"/>
    public const int DescriptionMaxLength = 400;

    /// <summary>
    /// Max length of tunnel event details.
    /// </summary>
    /// <seealso cref="TunnelEvent.Details"/>
    public const int EventDetailsMaxLength = 4000;

    /// <summary>
    /// Max number of properties in a tunnel event.
    /// </summary>
    public const int MaxEventProperties = 100;

    /// <summary>
    /// Max length of a single tunnel event property value.
    /// </summary>
    public const int EventPropertyValueMaxLength = 4000;

    /// <summary>
    /// Min length of a single tunnel or port tag.
    /// </summary>
    /// <seealso cref="Tunnel.Labels"/>
    /// <seealso cref="TunnelPort.Labels"/>
    public const int LabelMinLength = 1;

    /// <summary>
    /// Max length of a single tunnel or port tag.
    /// </summary>
    /// <seealso cref="Tunnel.Labels"/>
    /// <seealso cref="TunnelPort.Labels"/>
    public const int LabelMaxLength = 50;

    /// <summary>
    /// Maximum number of labels that can be applied to a tunnel or port.
    /// </summary>
    /// <seealso cref="Tunnel.Labels"/>
    /// <seealso cref="TunnelPort.Labels"/>
    public const int MaxLabels = 100;

    /// <summary>
    /// Min length of a tunnel domain.
    /// </summary>
    /// <seealso cref="Tunnel.Domain"/>
    public const int TunnelDomainMinLength = 4;

    /// <summary>
    /// Max length of a tunnel domain.
    /// </summary>
    /// <seealso cref="Tunnel.Domain"/>
    public const int TunnelDomainMaxLength = 180;

    /// <summary>
    /// Maximum number of items allowed in the tunnel ports array. The actual limit
    /// on number of ports that can be created may be much lower, and may depend on various resource
    /// limitations or policies.
    /// </summary>
    /// <seealso cref="Tunnel.Ports"/>
    public const int TunnelMaxPorts = 1000;

    /// <summary>
    /// Maximum number of access control entries (ACEs) in a tunnel or tunnel port
    /// access control list (ACL).
    /// </summary>
    /// <seealso cref="TunnelAccessControl.Entries" />
    public const int AccessControlMaxEntries = 40;

    /// <summary>
    /// Maximum number of subjects (such as user IDs) in a tunnel or tunnel port
    /// access control entry (ACE).
    /// </summary>
    /// <seealso cref="TunnelAccessControlEntry.Subjects" />
    public const int AccessControlMaxSubjects = 100;

    /// <summary>
    /// Max length of an access control subject or organization ID.
    /// </summary>
    /// <seealso cref="TunnelAccessControlEntry.Subjects"/>
    /// <seealso cref="TunnelAccessControlEntry.Organization"/>
    /// <seealso cref="TunnelAccessSubject.Id"/>
    /// <seealso cref="TunnelAccessSubject.OrganizationId"/>
    public const int AccessControlSubjectMaxLength = 200;

    /// <summary>
    /// Max length of an access control subject name, when resolving names to IDs.
    /// </summary>
    /// <seealso cref="TunnelAccessSubject.Name"/>
    public const int AccessControlSubjectNameMaxLength = 200;

    /// <summary>
    /// Maximum number of scopes in an access control entry.
    /// </summary>
    /// <seealso cref="TunnelAccessControlEntry.Scopes"/>
    public const int AccessControlMaxScopes = 10;

    /// <summary>
    /// Regular expression that can match or validate tunnel event name strings.
    /// </summary>
    public const string EventNamePattern = "^[a-z0-9_]{3,80}$";

    /// <summary>
    /// Regular expression that can match or validate tunnel event severity strings.
    /// </summary>
    public const string EventSeverityPattern = "^(info)|(warning)|(error)$";

    /// <summary>
    /// Regular expression that can match or validate tunnel event property name strings.
    /// </summary>
    public const string EventPropertyNamePattern = "^[a-zA-Z0-9_.]{3,200}$";

    /// <summary>
    /// Regular expression that can match or validate tunnel cluster ID strings.
    /// </summary>
    /// <remarks>
    /// Cluster IDs are alphanumeric; hyphens are not permitted.
    /// </remarks>
    /// <seealso cref="Tunnel.ClusterId"/>
    public const string ClusterIdPattern = "^(([a-z]{3,4}[0-9]{1,3})|asse|aue|brs|euw|use)$";

    /// <summary>
    /// Regular expression that can match or validate tunnel cluster ID strings.
    /// </summary>
    /// <remarks>
    /// Cluster IDs are alphanumeric; hyphens are not permitted.
    /// </remarks>
    /// <seealso cref="Tunnel.ClusterId"/>
    public static Regex ClusterIdRegex { get; } = new Regex(ClusterIdPattern);

    /// <summary>
    /// Regular expression that can match or validate a tunnel cluster ID as a hostname prefix.
    /// </summary>
    /// <remarks>
    /// Cluster IDs are alphanumeric; hyphens are not permitted.
    /// </remarks>
    /// <seealso cref="Tunnel.ClusterId"/>
    public static Regex ClusterIdPrefixRegex { get; } = new Regex(ClusterIdPattern.Replace("$", "\\."));

    /// <summary>
    /// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
    /// excluding vowels and 'y' (to avoid accidentally generating any random words).
    /// </summary>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string OldTunnelIdChars = "0123456789bcdfghjklmnpqrstvwxz";

    /// <summary>
    /// Regular expression that can match or validate tunnel ID strings.
    /// </summary>
    /// <remarks>
    /// Tunnel IDs are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string OldTunnelIdPattern = "[" + OldTunnelIdChars + "]{8}";

    /// <summary>
    /// Regular expression that can match or validate tunnel ID strings.
    /// </summary>
    /// <remarks>
    /// Tunnel IDs are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public static Regex OldTunnelIdRegex { get; } = new Regex(OldTunnelIdPattern);

    /// <summary>
    /// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
    /// excluding vowels and 'y' (to avoid accidentally generating any random words).
    /// </summary>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string NewTunnelIdChars = "0123456789abcdefghijklmnopqrstuvwxyz-";

    /// <summary>
    /// Regular expression that can match or validate tunnel ID strings.
    /// </summary>
    /// <remarks>
    /// Tunnel IDs are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string NewTunnelIdPattern = "[a-z0-9][a-z0-9-]{1,58}[a-z0-9]";

    /// <summary>
    /// Regular expression that can match or validate tunnel ID strings.
    /// </summary>
    /// <remarks>
    /// Tunnel IDs are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public static Regex NewTunnelIdRegex { get; } = new Regex(NewTunnelIdPattern);

    /// <summary>
    /// Characters that are valid in tunnel IDs. Includes numbers and lowercase letters,
    /// excluding vowels and 'y' (to avoid accidentally generating any random words).
    /// </summary>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string TunnelAliasChars = "0123456789bcdfghjklmnpqrstvwxz";

    /// <summary>
    /// Regular expression that can match or validate tunnel alias strings.
    /// </summary>
    /// <remarks>
    /// Tunnel Aliases are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public const string TunnelAliasPattern = "[" + TunnelAliasChars + "]{3,60}";

    /// <summary>
    /// Regular expression that can match or validate tunnel alias strings.
    /// </summary>
    /// <remarks>
    /// Tunnel Aliases are fixed-length and have a limited character set of
    /// numbers and lowercase letters (minus vowels and y).
    /// </remarks>
    /// <seealso cref="Tunnel.TunnelId"/>
    public static Regex TunnelAliasRegex { get; } = new Regex(TunnelAliasPattern);

    /// <summary>
    /// Regular expression that can match or validate tunnel names.
    /// </summary>
    /// <remarks>
    /// Tunnel names are alphanumeric and may contain hyphens. The pattern also
    /// allows an empty string because tunnels may be unnamed.
    /// </remarks>
    /// <seealso cref="Tunnel.Name"/>
    public const string TunnelNamePattern = "([a-z0-9][a-z0-9-]{1,58}[a-z0-9])|(^$)";

    /// <summary>
    /// Regular expression that can match or validate tunnel names.
    /// </summary>
    /// <remarks>
    /// Tunnel names are alphanumeric and may contain hyphens. The pattern also
    /// allows an empty string because tunnels may be unnamed.
    /// </remarks>
    /// <seealso cref="Tunnel.Name"/>
    public static Regex TunnelNameRegex { get; } = new Regex(TunnelNamePattern);

    /// <summary>
    /// Regular expression that can match or validate tunnel or port labels.
    /// </summary>
    /// <seealso cref="TunnelPort.Labels"/>
    public const string LabelPattern = "[\\w-=]{1,50}";

    /// <summary>
    /// Regular expression that can match or validate tunnel or port labels.
    /// </summary>
    /// <seealso cref="Tunnel.Labels"/>
    /// <seealso cref="TunnelPort.Labels"/>
    public static Regex LabelRegex { get; } = new Regex(LabelPattern);

    /// <summary>
    /// Regular expression that can match or validate tunnel domains.
    /// </summary>
    /// <remarks>
    /// The tunnel service may perform additional contextual validation at the time the domain
    /// is registered.
    /// </remarks>
    /// <seealso cref="Tunnel.Domain"/>
    public const string TunnelDomainPattern = "[0-9a-z][0-9a-z-.]{1,158}[0-9a-z]|(^$)";

    /// <summary>
    /// Regular expression that can match or validate tunnel domains.
    /// </summary>
    /// <remarks>
    /// The tunnel service may perform additional contextual validation at the time the domain
    /// is registered.
    /// </remarks>
    /// <seealso cref="Tunnel.Domain"/>
    public static Regex TunnelDomainRegex { get; } = new Regex(TunnelDomainPattern);

    /// <summary>
    /// Regular expression that can match or validate an access control subject or organization ID.
    /// </summary>
    /// <seealso cref="TunnelAccessControlEntry.Subjects"/>
    /// <seealso cref="TunnelAccessControlEntry.Organization"/>
    /// <seealso cref="TunnelAccessSubject.Id"/>
    /// <seealso cref="TunnelAccessSubject.OrganizationId"/>
    /// <remarks>
    /// The : and / characters are allowed because subjects may include IP addresses and ranges.
    /// The @ character is allowed because MSA subjects may be identified by email address.
    /// </remarks>
    public const string AccessControlSubjectPattern = "[0-9a-zA-Z-._:/@]{0,200}";

    /// <summary>
    /// Regular expression that can match or validate an access control subject or organization ID.
    /// </summary>
    /// <seealso cref="TunnelAccessControlEntry.Subjects"/>
    /// <seealso cref="TunnelAccessControlEntry.Organization"/>
    /// <seealso cref="TunnelAccessSubject.Id"/>
    /// <seealso cref="TunnelAccessSubject.OrganizationId"/>
    public static Regex AccessControlSubjectRegex { get; } = new Regex(AccessControlSubjectPattern);

    /// <summary>
    /// Regular expression that can match or validate an access control subject name, when resolving
    /// subject names to IDs.
    /// </summary>
    /// <seealso cref="TunnelAccessSubject.Name"/>
    /// <remarks>
    /// Note angle-brackets are only allowed when they wrap an email address as part of a
    /// formatted name with email. The service will block any other use of angle-brackets,
    /// to avoid any XSS risks.
    /// </remarks>
    public const string AccessControlSubjectNamePattern = "[ \\w\\d-.,/'\"_@()<>]{0,200}";

    /// <summary>
    /// Regular expression that can match or validate an access control subject name, when resolving
    /// subject names to IDs.
    /// </summary>
    /// <seealso cref="TunnelAccessSubject.Name"/>
    public static Regex AccessControlSubjectNameRegex { get; } = new Regex(AccessControlSubjectNamePattern);

    /// <summary>
    /// Validates <paramref name="clusterId"/> and returns true if it is a valid cluster ID, otherwise false.
    /// </summary>
    public static bool IsValidClusterId(string clusterId)
    {
        if (string.IsNullOrEmpty(clusterId))
        {
            return false;
        }

        var m = ClusterIdRegex.Match(clusterId);
        return m.Index == 0 && m.Length == clusterId.Length;
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and returns true if it is a valid tunnel id, otherwise, false.
    /// </summary>
    public static bool IsValidOldTunnelId(string tunnelId)
    {
        if (string.IsNullOrEmpty(tunnelId) || tunnelId.Length != OldTunnelIdLength)
        {
            return false;
        }

        var m = OldTunnelIdRegex.Match(tunnelId);
        return m.Index == 0 && m.Length == tunnelId.Length;
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and returns true if it is a valid tunnel id, otherwise, false.
    /// </summary>
    public static bool IsValidNewTunnelId(string tunnelId)
    {
        if (string.IsNullOrEmpty(tunnelId) || tunnelId.Length < NewTunnelIdMinLength || tunnelId.Length > NewTunnelIdMaxLength)
        {
            return false;
        }

        var m = NewTunnelIdRegex.Match(tunnelId);
        return m.Index == 0 && m.Length == tunnelId?.Length && !IsValidTunnelAlias(tunnelId);
    }

    /// <summary>
    /// Validates <paramref name="alias"/> and returns true if it is a valid tunnel alias, otherwise, false.
    /// </summary>
    public static bool IsValidTunnelAlias(string alias)
    {
        if (string.IsNullOrEmpty(alias) || alias.Length != TunnelAliasLength)
        {
            return false;
        }

        var m = TunnelAliasRegex.Match(alias);
        return (m.Index == 0 && m.Length == alias.Length);
    }

    /// <summary>
    /// Validates <paramref name="tunnelName"/> and returns true if it is a valid tunnel name, otherwise, false.
    /// </summary>
    public static bool IsValidTunnelName(string tunnelName)
    {
        if (string.IsNullOrEmpty(tunnelName))
        {
            return false;
        }
        if (tunnelName.EndsWith("-inspect", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var m = TunnelNameRegex.Match(tunnelName);
        return m.Index == 0 && m.Length == tunnelName.Length && !IsValidTunnelAlias(tunnelName);
    }

    /// <summary>
    /// Validates <paramref name="tag"/> and returns true if it is a valid tunnel tag, otherwise, false.
    /// </summary>
    public static bool IsValidTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        var m = LabelRegex.Match(tag);
        return m.Index == 0 && m.Length == tag.Length;
    }

    /// <summary>
    /// Validates <paramref name="tunnelIdOrName"/> and returns true if it is a valid tunnel id or name.
    /// </summary>
    public static bool IsValidTunnelIdOrName(string tunnelIdOrName)
    {
        if (string.IsNullOrEmpty(tunnelIdOrName))
        {
            return false;
        }

        // Tunnel ID Regex is a subset of Tunnel name Regex
        var m = TunnelNameRegex.Match(tunnelIdOrName);
        return m.Index == 0 && m.Length == tunnelIdOrName.Length;
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and throws exception if it is null or not a valid tunnel id.
    /// Returns <paramref name="tunnelId"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelId"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelId"/> is not a valid tunnel id.</exception>
    public static string ValidateOldTunnelId(string tunnelId, string? paramName = default)
    {
        paramName ??= nameof(tunnelId);

        if (tunnelId == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (!IsValidOldTunnelId(tunnelId))
        {
            throw new ArgumentException("Invalid tunnel id", paramName);
        }

        return tunnelId;
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and throws exception if it is null or not a valid tunnel id.
    /// Returns <paramref name="tunnelId"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelId"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelId"/> is not a valid tunnel id.</exception>
    public static string ValidateNewOrOldTunnelId(string tunnelId, string? paramName = default)
    {
        try {
            return ValidateNewTunnelId(tunnelId, paramName);
        }
        catch (ArgumentException)
        {
            return ValidateOldTunnelId(tunnelId, paramName);
        }
    }

    /// <summary>
    /// Validates <paramref name="tunnelId"/> and throws exception if it is null or not a valid tunnel id.
    /// Returns <paramref name="tunnelId"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelId"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelId"/> is not a valid tunnel id.</exception>
    public static string ValidateNewTunnelId(string tunnelId, string? paramName = default)
    {
        paramName ??= nameof(tunnelId);

        if (tunnelId == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (!IsValidNewTunnelId(tunnelId))
        {
            throw new ArgumentException("Invalid tunnel id", paramName);
        }

        if (IsValidTunnelAlias(tunnelId))
        {
            throw new ArgumentException("Tunnel id must either be not 8 characters long or have a vowel", paramName);
        }

        return tunnelId;
    }

    /// <summary>
    /// Validates <paramref name="tunnelAlias"/> and throws exception if it is null or not a valid tunnel id.
    /// Returns <paramref name="tunnelAlias"/> back if it's a valid tunnel id.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnelAlias"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tunnelAlias"/> is not a valid tunnel id.</exception>
    public static string ValidateTunnelAlias(string tunnelAlias, string? paramName = default)
    {
        paramName ??= nameof(tunnelAlias);

        if (tunnelAlias == null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (!IsValidTunnelAlias(tunnelAlias))
        {
            throw new ArgumentException("Invalid tunnel id", paramName);
        }

        return tunnelAlias;
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
