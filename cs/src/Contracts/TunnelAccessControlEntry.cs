// <copyright file="TunnelAccessControlEntry.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for an access control entry on a <see cref="Tunnel"/> or
    /// <see cref="TunnelPort"/>.
    /// </summary>
    /// <remarks>
    /// An access control entry (ACE) grants or denies one or more access scopes to one
    /// or more subjects. Tunnel ports inherit access control entries from their
    /// tunnel, and they may have additional port-specific entries that
    /// augment or override those access rules.
    /// </remarks>
    [DebuggerDisplay("{ToString(),nq}")]
    public class TunnelAccessControlEntry
    {
        /// <summary>
        /// Constants for well-known identity providers.
        /// </summary>
        public static class Providers
        {
            /// <summary>Microsoft (AAD) identity provider.</summary>
            public const string Microsoft = "microsoft";

            /// <summary>GitHub identity provider.</summary>
            public const string GitHub = "github";

            /// <summary>SSH public keys.</summary>
            public const string Ssh = "ssh";

            /// <summary>IPv4 addresses.</summary>
            public const string IPv4 = "ipv4";

            /// <summary>IPv6 addresses.</summary>
            public const string IPv6 = "ipv6";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelAccessControlEntry"/> class.
        /// </summary>
        public TunnelAccessControlEntry()
        {
            Scopes = Array.Empty<string>();
            Subjects = Array.Empty<string>();
        }

        /// <summary>
        /// Gets or sets the access control entry type.
        /// </summary>
        public TunnelAccessControlEntryType Type { get; set; }

        /// <summary>
        /// Gets or sets the provider of the subjects in this access control entry. The provider
        /// impacts how the subject identifiers are resolved and displayed. The provider may be an
        /// identity provider such as AAD, or a system or standard such as "ssh" or "ipv4".
        /// </summary>
        /// <remarks>
        /// For user, group, or org ACEs, this value is the name of the identity provider
        /// of the user/group/org IDs. It may be one of the well-known provider names in
        /// <see cref="Providers" />, or (in the future) a custom identity provider.
        ///
        /// For public key ACEs, this value is the type of public key, e.g. "ssh".
        ///
        /// For IP address range ACEs, this value is the IP addrss version, e.g. "ipv4" or "ipv6".
        ///
        /// For anonymous ACEs, this value is null.
        /// </remarks>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is an access control entry on a tunnel
        /// port that is inherited from the tunnel's access control list.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsInherited { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this entry is a deny rule that blocks access
        /// to the specified users. Otherwise it is an allow role.
        /// </summary>
        /// <remarks>
        /// All deny rules (including inherited rules) are processed after all allow rules.
        /// Therefore a deny rule cannot be overridden by an allow rule that is later in the list
        /// or on a more-specific resource.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsDeny { get; set; }

        /// <summary>
        /// Gets or sets an optional organization context for all subjects of this entry. The use
        /// and meaning of this value depends on the <see cref="Type" /> and <see cref="Provider" />
        /// of this entry.
        /// </summary>
        /// <remarks>
        /// For AAD users and group ACEs, this value is the AAD tenant ID. It is not currently used
        /// with any other types of ACEs.
        /// </remarks>
        public string? Organization { get; set; }

        /// <summary>
        /// Gets or sets the subjects for the entry, such as user or group IDs. The format of the
        /// values depends on the <see cref="Type" /> and <see cref="Provider" /> of this entry.
        /// </summary>
        public string[] Subjects { get; set; }

        /// <summary>
        /// Gets or sets the access scopes that this entry grants or denies to the subjects.
        /// </summary>
        /// <remarks>
        /// These must be one or more values from <see cref="TunnelAccessScopes" />.
        /// </remarks>
        public string[] Scopes { get; set; }

        /// <summary>
        /// Creates new access control entry that is a (shallow) copy of the specified ACE,
        /// but with <see cref="IsInherited" /> set to true.
        /// </summary>
        public static TunnelAccessControlEntry Inherit(TunnelAccessControlEntry ace)
        {
            return new TunnelAccessControlEntry
            {
                Type = ace.Type,
                Provider = ace.Provider,
                IsDeny = ace.IsDeny,
                Organization = ace.Organization,
                Subjects = ace.Subjects,
                Scopes = ace.Scopes,
                IsInherited = true,
            };
        }

        /// <summary>
        /// Gets a compact textual representation of the access control entry.
        /// </summary>
        public override string ToString()
        {
            var s = new StringBuilder();

            if (IsInherited)
            {
                s.Append("Inherited: ");
            }

            s.Append(IsDeny ? '-' : '+');
            s.Append(GetEntryTypeLabel(Type, Provider, Subjects.Length != 1));

            if (Scopes.Length > 0)
            {
                s.Append($" [{string.Join(", ", Scopes)}]");
            }

            if (Subjects.Length > 0)
            {
                s.Append($" ({string.Join(", ", Subjects)})");
            }

            return s.ToString();
        }

        private static string GetEntryTypeLabel(
            TunnelAccessControlEntryType entryType,
            string? provider,
            bool plural)
        {
            if (entryType == TunnelAccessControlEntryType.Anonymous)
            {
                plural = false;
            }

            var label = entryType switch
            {
                TunnelAccessControlEntryType.Anonymous => "Anonymous",
                TunnelAccessControlEntryType.Users => "User",
                TunnelAccessControlEntryType.Groups =>
                    provider == Providers.GitHub ? "Team" : "Group",
                TunnelAccessControlEntryType.Organizations =>
                    provider == Providers.Microsoft ? "Tenant" : "Org",
                TunnelAccessControlEntryType.Repositories => "Repo",
                TunnelAccessControlEntryType.PublicKeys => "Key",
                TunnelAccessControlEntryType.IPAddressRanges => "IP Range",
                _ => entryType.ToString(),
            };

            if (plural)
            {
                label += "s";
            }

            if (!string.IsNullOrEmpty(provider))
            {
                label = provider switch
                {
                    Providers.Microsoft => $"AAD {label}",
                    Providers.GitHub => $"GitHub {label}",
                    Providers.Ssh => $"SSH {label}",
                    Providers.IPv4 => label.Replace("IP", "IPv4"),
                    Providers.IPv6 => label.Replace("IP", "IPv6"),
                    _ => $"{label} ({provider})",
                };
            }

            return label;
        }
    }
}
