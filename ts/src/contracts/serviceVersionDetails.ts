// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ServiceVersionDetails.cs
/* eslint-disable */

/**
 * Data contract for service version details.
 */
export interface ServiceVersionDetails {
    /**
     * Gets or sets the version of the service. E.g. "1.0.6615.53976". The version
     * corresponds to the build number.
     */
    version?: string;

    /**
     * Gets or sets the commit ID of the service.
     */
    commitId?: string;

    /**
     * Gets or sets the commit date of the service.
     */
    commitDate?: string;

    /**
     * Gets or sets the cluster ID of the service that handled the request.
     */
    clusterId?: string;

    /**
     * Gets or sets the Azure location of the service that handled the request.
     */
    azureLocation?: string;
}
