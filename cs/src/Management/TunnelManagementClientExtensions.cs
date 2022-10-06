// <copyright file="TunnelManagementClientExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Extension methods <see cref="ITunnelManagementClient"/>.
    /// </summary>
    public static class TunnelManagementClientExtensions
    {
        /// <summary>
        /// Looks up and formats subject names for display.
        /// </summary>
        /// <param name="managementClient">The tunnel management client that is used
        /// to call the service.</param>
        /// <param name="accessControl">Access control entries to be formatted. Items in
        /// <see cref="TunnelAccessControlEntry.Subjects"/> must be subject IDs. (For AAD the
        /// IDs are user or group object ID GUIDs; for GitHub they are user or team ID integers.)
        /// The subjects are then updated in-place to the formatted subject names.</param>
        /// <param name="provider">Required identity provider of subjects to format. The
        /// <paramref name="managementClient"/> must authenticate using this provider.</param>
        /// <param name="organization">Optional organization of subjects to format. If specified,
        /// the <paramref name="managementClient"/> must authenticate in this organization.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>List of subjects that could not be formatted because the IDs were
        /// not found, or an empty list if all subjects were formatted successfully.</returns>
        public static async Task<IEnumerable<TunnelAccessSubject>> FormatSubjectsAsync(
            this ITunnelManagementClient managementClient,
            IEnumerable<TunnelAccessControlEntry> accessControl,
            string provider,
            string? organization,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(managementClient, nameof(managementClient));
            Requires.NotNull(accessControl, nameof(accessControl));
            Requires.NotNullOrEmpty(provider, nameof(provider));

            var subjects = new List<TunnelAccessSubject>();
            var aceIndexes = new List<(TunnelAccessControlEntry, int)>();

            foreach (var ace in accessControl)
            {
                // With AAD, a user can only query info about their current organization.
                // With GH, a user can be a member of multiple orgs so there is no "current".
                if (ace.Provider == provider &&
                    (ace.Provider != TunnelAccessControlEntry.Providers.Microsoft ||
                    ace.Organization == organization ||
                    ace.Type == TunnelAccessControlEntryType.Organizations))
                {
                    bool isOrganizationIdRequired =
                        ace.Type == TunnelAccessControlEntryType.Groups &&
                        ace.Provider == TunnelAccessControlEntry.Providers.GitHub;

                    for (int i = 0; i < ace.Subjects.Length; i++)
                    {
                        subjects.Add(new TunnelAccessSubject
                        {
                            Type = ace.Type,
                            Id = ace.Subjects[i],
                            OrganizationId = isOrganizationIdRequired ? ace.Organization : null,
                        });
                        aceIndexes.Add((ace, i));
                    }
                }
            }

            var unformattedSubjects = new List<TunnelAccessSubject>(subjects.Count);

            if (subjects.Count > 0)
            {
                var formattedSubjects = await managementClient.FormatSubjectsAsync(
                    subjects.ToArray(), options: null, cancellation);
                for (int i = 0; i < formattedSubjects.Length; i++)
                {
                    var formattedName = formattedSubjects[i].Name;
                    if (!string.IsNullOrEmpty(formattedName))
                    {
                        var (ace, subjectIndex) = aceIndexes[i];
                        ace.Subjects[subjectIndex] = formattedName;
                    }
                    else
                    {
                        unformattedSubjects.Add(formattedSubjects[i]);
                    }
                }
            }

            return unformattedSubjects;
        }

        /// <summary>
        /// Resolves partial or full subject display names or emails to IDs.
        /// </summary>
        /// <param name="managementClient">The tunnel management client that is used
        /// to call the service.</param>
        /// <param name="accessControl">Access control entries to be resolved. Items in
        /// <see cref="TunnelAccessControlEntry.Subjects"/> must be partial or full subject names.
        /// (For AAD the subjects are user or group emails or display names; for GitHub they are
        /// user or team names or display names.) The subjects are then updated in-place to the
        /// resolved subject IDs.</param>
        /// <param name="provider">Required identity provider of subjects to resolve. The
        /// <paramref name="managementClient"/> must authenticate using this provider.</param>
        /// <param name="organization">Optional organization of subjects to resolve. If specified,
        /// the <paramref name="managementClient"/> must authenticate in this organization.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>List of subjects that could not be resolved or had multiple partial
        /// matches, or an empty list if all subjects were resolved successfully.</returns>
        public static async Task<IEnumerable<TunnelAccessSubject>> ResolveSubjectsAsync(
            this ITunnelManagementClient managementClient,
            IEnumerable<TunnelAccessControlEntry> accessControl,
            string provider,
            string? organization,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(managementClient, nameof(managementClient));
            Requires.NotNull(accessControl, nameof(accessControl));
            Requires.NotNullOrEmpty(provider, nameof(provider));

            var subjects = new List<TunnelAccessSubject>();
            var aceIndexes = new List<(TunnelAccessControlEntry, int)>();

            foreach (var ace in accessControl)
            {
                if (ace.Provider == provider &&
                    (ace.Organization == organization ||
                    ace.Type == TunnelAccessControlEntryType.Organizations))
                {
                    for (int i = 0; i < ace.Subjects.Length; i++)
                    {
                        subjects.Add(new TunnelAccessSubject
                        {
                            Type = ace.Type,
                            Name = ace.Subjects[i],
                        });
                        aceIndexes.Add((ace, i));
                    }
                }
            }

            var unresolvedSubjects = new List<TunnelAccessSubject>(subjects.Count);
            if (subjects.Count > 0)
            {
                var resolvedSubjects = await managementClient.ResolveSubjectsAsync(
                    subjects.ToArray(), options: null, cancellation);
                for (int i = 0; i < resolvedSubjects.Length; i++)
                {
                    var resolvedId = resolvedSubjects[i].Id;
                    var resolvedOrgId = resolvedSubjects[i].OrganizationId;
                    if (!string.IsNullOrEmpty(resolvedId))
                    {
                        var (ace, subjectIndex) = aceIndexes[i];
                        ace.Subjects[subjectIndex] = resolvedId;

                        if (!string.IsNullOrEmpty(resolvedOrgId))
                        {
                            if (ace.Organization == null)
                            {
                                ace.Organization = resolvedOrgId;
                            }
                            else if (ace.Organization != resolvedOrgId)
                            {
                                throw new ArgumentException(
                                    "Multiple teams must be in the same organization.");
                            }
                        }
                    }
                    else
                    {
                        unresolvedSubjects.Add(resolvedSubjects[i]);
                    }
                }
            }

            return unresolvedSubjects;
        }
    }
}
