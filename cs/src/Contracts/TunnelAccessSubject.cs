// <copyright file="TunnelAccessSubject.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Properties about a subject of a tunnel access control entry (ACE), used when resolving
    /// subject names to IDs when creating new ACEs, or formatting subject IDs to names when
    /// displaying existing ACEs.
    /// </summary>
    public class TunnelAccessSubject
    {
        /// <summary>
        /// Gets or sets the type of subject, e.g. user, group, or organization.
        /// </summary>
        public TunnelAccessControlEntryType Type { get; set; }

        /// <summary>
        /// Gets or sets the subject ID.
        /// </summary>
        /// <remarks>The ID is typically a guid or integer that is unique within the scope of
        /// the identity provider or organization, and never changes for that subject.</remarks>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the subject organization ID, which may be required if an organization is
        /// not implied by the authentication context.
        /// </summary>
        public string? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets the partial or full subject name.
        /// </summary>
        /// <remarks>
        /// When resolving a subject name to ID, a partial name may be provided, and the full name
        /// is returned if the partial name was successfully resolved. When formatting
        /// a subject ID to name, the full name is returned if the ID was found.
        /// </remarks>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets an array of possible subject matches, if a partial name was provided
        /// and did not resolve to a single subject.
        /// </summary>
        /// <remarks>
        /// This property applies only when resolving subject names to IDs.
        /// </remarks>
        public TunnelAccessSubject[]? Matches { get; set; }
    }
}
