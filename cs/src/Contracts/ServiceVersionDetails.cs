// <copyright file="ServiceVersionDetails.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Data contract for service version details.
    /// </summary>
    public class ServiceVersionDetails
    {
        /// <summary>
        /// Gets or sets the version of the service. E.g. "1.0.6615.53976". The version corresponds to the build number.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the commit ID of the service.
        /// </summary>
        public string? CommitId { get; set; }

        /// <summary>
        /// Gets or sets the commit date of the service.
        /// </summary>
        public string? CommitDate { get; set; }

        /// <summary>
        /// Gets or sets the cluster ID of the service that handled the request.
        /// </summary>
        public string? ClusterId { get; set; }

        /// <summary>
        /// Gets or sets the Azure location of the service that handled the request.
        /// </summary>
        public string? AzureLocation { get; set; }
    }
}
