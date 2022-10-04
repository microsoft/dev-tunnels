// <copyright file="TunnelAccessControlEntryType.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Specifies the type of <see cref="TunnelAccessControlEntry"/>.
    /// </summary>
    public enum TunnelAccessControlEntryType
    {
        /// <summary>
        /// Uninitialized access control entry type.
        /// </summary>
        None = 0,

        /// <summary>
        /// The access control entry refers to all anonymous users.
        /// </summary>
        Anonymous,

        /// <summary>
        /// The access control entry is a list of user IDs that are allowed (or denied) access.
        /// </summary>
        Users,

        /// <summary>
        /// The access control entry is a list of groups IDs that are allowed (or denied) access.
        /// </summary>
        Groups,

        /// <summary>
        /// The access control entry is a list of organization IDs that are allowed (or denied)
        /// access.
        /// </summary>
        /// <remarks>
        /// All users in the organizations are allowed (or denied) access, unless overridden by
        /// following group or user rules.
        /// </remarks>
        Organizations,

        /// <summary>
        /// The access control entry is a list of repositories. Users are allowed access to
        /// the tunnel if they have access to the repo.
        /// </summary>
        Repositories,

        /// <summary>
        /// The access control entry is a list of public keys. Users are allowed access if
        /// they can authenticate using a private key corresponding to one of the public keys.
        /// </summary>
        PublicKeys,

        /// <summary>
        /// The access control entry is a list of IP address ranges that are allowed (or denied)
        /// access to the tunnel. Ranges can be IPv4, IPv6, or Azure service tags.
        /// </summary>
        IPAddressRanges,
    }
}
