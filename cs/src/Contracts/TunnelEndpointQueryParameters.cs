// <copyright file="TunnelEndpointQueryParameters.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// The query parameters that can be passed to the update/delete TunnelEndpoint api.
/// </summary>
public static class TunnelEndpointQueryParameters
{
    /// <summary>
    /// Include ssh gateway public key in the TunnelEndpoint response.
    /// This will be set to true by the SDK if the tunnel has ssh ports.
    /// </summary>
    public static string IncludeSshGatewayPublicKey { get; } = "includeSshGatewayPublicKey";
}
