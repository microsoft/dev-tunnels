// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/ServiceVersionDetails.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Data contract for service version details.
 */
public class ServiceVersionDetails {
    /**
     * Gets or sets the version of the service. E.g. "1.0.6615.53976". The version
     * corresponds to the build number.
     */
    @Expose
    public String version;

    /**
     * Gets or sets the commit ID of the service.
     */
    @Expose
    public String commitId;

    /**
     * Gets or sets the commit date of the service.
     */
    @Expose
    public String commitDate;

    /**
     * Gets or sets the cluster ID of the service that handled the request.
     */
    @Expose
    public String clusterId;

    /**
     * Gets or sets the Azure location of the service that handled the request.
     */
    @Expose
    public String azureLocation;
}
