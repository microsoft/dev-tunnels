// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelOptions.cs
/* eslint-disable */

/**
 * Data contract for {@link Tunnel} or {@link TunnelPort} options.
 */
export interface TunnelOptions {
    /**
     * Gets or sets a value indicating whether web-forwarding of this tunnel can run on
     * any cluster (region) without redirecting to the home cluster. This is only
     * applicable if the tunnel has a name and web-forwarding uses it.
     */
    isGloballyAvailable?: boolean;
}
