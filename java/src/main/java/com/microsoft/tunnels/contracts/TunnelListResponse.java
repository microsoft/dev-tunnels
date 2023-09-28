// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelListResponse.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Data contract for response of a list tunnel call.
 */
public class TunnelListResponse {
    /**
     * List of tunnels
     */
    @Expose
    public Tunnel[] value;

    /**
     * Link to get next page of results
     */
    @Expose
    public String nextLink;
}
