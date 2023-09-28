// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelPortListResponse.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Data contract for response of a list tunnel ports call.
 */
public class TunnelPortListResponse {
    /**
     * List of tunnels
     */
    @Expose
    public TunnelPort[] value;

    /**
     * Link to get next page of results
     */
    @Expose
    public String nextLink;
}
