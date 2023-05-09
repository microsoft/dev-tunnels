// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/NamedRateStatus.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * A named {@link RateStatus}.
 */
public class NamedRateStatus extends RateStatus {
    /**
     * The name of the rate status.
     */
    @Expose
    public String name;
}
