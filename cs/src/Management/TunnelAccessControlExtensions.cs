// <copyright file="TunnelAccessControlExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Extension methods for working with <see cref="TunnelAccessControl"/> and
    /// its list of entries.
    /// </summary>
    public static class TunnelAccessControlExtensions
    {
        /// <summary>
        /// Resets to default access control by removing all non-inherited entries.
        /// </summary>
        public static void Reset(this TunnelAccessControl accessControl)
        {
            Requires.NotNull(accessControl, nameof(accessControl));

            lock (accessControl)
            {
                var entries = accessControl.ToList();
                entries.RemoveAll((ace) => !ace.IsInherited);
                accessControl.Entries = entries.ToArray();
            }
        }

        /// <summary>
        /// Adds an entry to the access control list that allows access to a list of users
        /// or credentials, overriding any inherited or existing entry for the same users.
        /// </summary>
        public static void Allow(
            this TunnelAccessControl accessControl,
            TunnelAccessControlEntryType entryType,
            string? provider,
            string[] subjects,
            params string[] scopes)
        {
            Requires.NotNull(accessControl, nameof(accessControl));

            ValidateEntryType(entryType);
            ValidateSubjects(subjects, entryType);
            ValidateScopes(scopes);

            lock (accessControl)
            {
                var entries = accessControl.ToList();
                entries.Add(new TunnelAccessControlEntry
                {
                    Type = entryType,
                    Provider = provider,
                    Scopes = new List<string>(scopes).ToArray(),
                    Subjects = new List<string>(subjects).ToArray(),
                    IsDeny = false,
                });
                accessControl.Entries = entries.ToArray();
            }
        }

        /// <summary>
        /// Adds an entry to the access control list that denies access to a list of users
        /// or credentials, overriding any inherited or existing entry for the same users.
        /// </summary>
        public static void Deny(
            this TunnelAccessControl accessControl,
            TunnelAccessControlEntryType entryType,
            string? provider,
            string[] subjects,
            params string[] scopes)
        {
            Requires.NotNull(accessControl, nameof(accessControl));
            ValidateEntryType(entryType);
            ValidateSubjects(subjects, entryType);
            ValidateScopes(scopes);

            lock (accessControl)
            {
                var entries = accessControl.ToList();
                entries.Add(new TunnelAccessControlEntry
                {
                    Type = entryType,
                    Provider = provider,
                    Scopes = new List<string>(scopes).ToArray(),
                    Subjects = new List<string>(subjects).ToArray(),
                    IsDeny = true,
                });
                accessControl.Entries = entries.ToArray();
            }
        }

        /// <summary>
        /// Checks whether a subject is allowed a specified scope of access.
        /// </summary>
        /// <returns>
        /// True if access is allowed, false if access is denied, or null if there is
        /// no applicable access control entry.
        /// </returns>
        /// <remarks>
        /// Entries are evaluated in order, with later entries overriding earlier entries.
        /// All allow rules are processed first, followed by all deny rules. This ensures an
        /// inherited deny rule cannot be overridden at a lower level.
        ///
        /// Warning: This does not consider whether a user may be allowed (or denied) access due to
        /// group or organization membership. It only scans access control entries of the specified
        /// type. It may be necessary to separately check group or org access control entry types.
        ///
        /// Generally no entry (null return value) should be handled the same as denial,
        /// but the difference might be relevant for logging/auditing.
        /// </remarks>
        public static bool? IsAllowed(
            this TunnelAccessControl accessControl,
            TunnelAccessControlEntryType entryType,
            string subject,
            string scope)
        {
            Requires.NotNull(accessControl, nameof(accessControl));
            ValidateEntryType(entryType);

            if (entryType != TunnelAccessControlEntryType.Anonymous)
            {
                Requires.NotNullOrEmpty(subject, nameof(subject));
            }

            ValidateScopes(new[] { scope });

            bool? allowed = null;

            foreach (var ace in accessControl)
            {
                if (!ace.IsDeny && IsEntryMatch(ace, entryType, subject, scope))
                {
                    allowed = true;
                    break;
                }
            }

            foreach (var ace in accessControl)
            {
                if (ace.IsDeny && IsEntryMatch(ace, entryType, subject, scope))
                {
                    allowed = false;
                    break;
                }
            }

            return allowed;
        }

        /// <summary>
        /// Checks if an access control entry matches the specified entry type, subject, and scope.
        /// </summary>
        private static bool IsEntryMatch(
            TunnelAccessControlEntry ace,
            TunnelAccessControlEntryType entryType,
            string subject,
            string scope)
        {
            return ace.Type == entryType &&
                (string.IsNullOrEmpty(subject) ||
                ace.Subjects.Contains(subject) != ace.IsInverse) &&
                ace.Scopes.Contains(scope);
        }

        /// <summary>
        /// Adds an access control entry that allows anonymous users, overriding any
        /// inherited or existing entry for anonymous users.
        /// </summary>
        public static void AllowAnonymous(this TunnelAccessControl accessControl, string scope)
        {
            Allow(
                accessControl,
                TunnelAccessControlEntryType.Anonymous,
                provider: null,
                Array.Empty<string>(),
                scope);
        }

        /// <summary>
        /// Adds an access control entry that denies anonymous users, overriding any
        /// inherited or existing entry for anonymous users.
        /// </summary>
        public static void DenyAnonymous(this TunnelAccessControl accessControl, string scope)
        {
            Deny(
                accessControl,
                TunnelAccessControlEntryType.Anonymous,
                provider: null,
                Array.Empty<string>(),
                scope);
        }

        /// <summary>
        /// Checks whether anonymous users are allowed access.
        /// </summary>
        /// <returns>
        /// True if access is allowed, false if access is denied, or null if there is
        /// no applicable access control entry.
        /// </returns>
        /// <remarks>
        /// Entries are evaluated in order, with later entries overriding earlier entries.
        ///
        /// Generally no entry (null return value) should be handled the same as denial,
        /// but the difference might be relevant for logging/auditing.
        /// </remarks>
        public static bool? IsAnonymousAllowed(this TunnelAccessControl accessControl, string scope)
        {
            return IsAllowed(
                accessControl,
                TunnelAccessControlEntryType.Anonymous,
                string.Empty,
                scope);
        }

        private static void ValidateEntryType(TunnelAccessControlEntryType entryType)
        {
            Requires.Argument(
                Enum.IsDefined(typeof(TunnelAccessControlEntryType), entryType),
                nameof(entryType),
                "Entry type is invalid.");
            Requires.Argument(
                entryType != TunnelAccessControlEntryType.None,
                nameof(entryType),
                "Entry type is uninitialized.");
        }

        private static void ValidateSubjects(
            string[] subjects,
            TunnelAccessControlEntryType entryType)
        {
            Requires.NotNull(subjects, nameof(subjects));
            if (entryType == TunnelAccessControlEntryType.Anonymous)
            {
                Requires.Argument(
                    subjects.Length == 0,
                    nameof(subjects),
                    "Subjects array must be empty for an anonymous entry.");
            }
            else
            {
                Requires.Argument(
                    subjects.Length > 0,
                    nameof(subjects),
                    "Subjects array must not be empty.");
            }
        }

        private static void ValidateScopes(string[] scopes)
        {
            Requires.NotNull(scopes, nameof(scopes));
            Requires.Argument(
                scopes.Length > 0,
                nameof(scopes),
                "Scopes array must not be empty.");
            TunnelAccessControl.ValidateScopes(scopes);
        }
    }
}
