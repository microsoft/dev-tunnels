// <copyright file="TunnelAuthenticationSchemes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Defines string constants for authentication schemes supported by tunnel service APIs.
/// </summary>
public class TunnelAuthenticationSchemes
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
}
