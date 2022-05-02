// Generated from ../../../../../../../../cs/src/Contracts/TunnelAuthenticationSchemes.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Defines string constants for authentication schemes supported by tunnel service APIs.
 */
public class TunnelAuthenticationSchemes {
    /**
     * Authentication scheme for AAD (or Microsoft account) access tokens.
     */
    @Expose
    public static String aad = "aad";

    /**
     * Authentication scheme for GitHub access tokens.
     */
    @Expose
    public static String gitHub = "github";

    /**
     * Authentication scheme for tunnel access tokens.
     */
    @Expose
    public static String tunnel = "tunnel";
}
