// <copyright file="TunnelAuthenticationSchemes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Defines string constants for authentication schemes supported by tunnel service APIs.
/// </summary>
public static class TunnelAuthenticationSchemes
{
    /// <summary>
    /// Authentication scheme for AAD (or Microsoft account) access tokens.
    /// </summary>
    public const string Aad = "aad";

    /// <summary>
    /// Authentication scheme for GitHub access tokens.
    /// </summary>
    public const string GitHub = "github";

    /// <summary>
    /// Authentication scheme for tunnel access tokens.
    /// </summary>
    public const string Tunnel = "tunnel";

    /// <summary>
    /// Authentication scheme for tunnelPlan access tokens.
    /// </summary>
    public const string TunnelPlan = "tunnelplan";
}
