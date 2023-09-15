// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelListByRegion.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Tunnel list by region.
 */
public class TunnelListByRegion {
    /**
     * Azure region name.
     */
    @Expose
    public String regionName;

    /**
     * Cluster id in the region.
     */
    @Expose
    public String clusterId;

    /**
     * List of tunnels.
     */
    @Expose
    public Tunnel[] value;

    /**
     * Error detail if getting list of tunnels in the region failed.
     */
    @Expose
    public ErrorDetail error;
}
