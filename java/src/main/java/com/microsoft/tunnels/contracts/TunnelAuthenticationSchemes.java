// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelAuthenticationSchemes.cs

package com.microsoft.tunnels.contracts;

/**
 * Defines string constants for authentication schemes supported by tunnel service APIs.
 */
public class TunnelAuthenticationSchemes {
    /**
     * Authentication scheme for AAD (or Microsoft account) access tokens.
     */
    public static final String aad = "aad";

    /**
     * Authentication scheme for GitHub access tokens.
     */
    public static final String gitHub = "github";

    /**
     * Authentication scheme for tunnel access tokens.
     */
    public static final String tunnel = "tunnel";

    /**
     * Authentication scheme for tunnelPlan access tokens.
     */
    public static final String tunnelPlan = "tunnelplan";
}
