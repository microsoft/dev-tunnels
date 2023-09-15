// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelListByRegionResponse.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Data contract for response of a list tunnel by region call.
 */
public class TunnelListByRegionResponse {
    /**
     * List of tunnels
     */
    @Expose
    public TunnelListByRegion[] value;

    /**
     * Link to get next page of results.
     */
    @Expose
    public String nextLink;
}
