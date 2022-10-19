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

    /**
     * Gets or sets a value for `Host` header rewriting to use in web-forwarding of this
     * tunnel or port. By default, with this property null or empty, web-forwarding uses
     * "localhost" to rewrite the `Host` header. Web-fowarding will use this property
     * instead if it is not null or empty, . Port-level option, if set, takes precedence
     * over this option on the tunnel level.
     */
    hostHeader?: string;
}
