// <copyright file="TunnelServiceProperties.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Provides environment-dependent properties about the service.
/// </summary>
public class TunnelServiceProperties
{
    /// <summary>
    /// Global DNS name of the production tunnel service.
    /// </summary>
    internal const string ProdDnsName = "global.rel.tunnels.api.visualstudio.com";

    /// <summary>
    /// Global DNS name of the pre-production tunnel service.
    /// </summary>
    internal const string PpeDnsName = "global.rel.tunnels.ppe.api.visualstudio.com";

    /// <summary>
    /// Global DNS name of the development tunnel service.
    /// </summary>
    internal const string DevDnsName = "global.ci.tunnels.dev.api.visualstudio.com";

    /// <summary>
    /// First-party app ID: `Visual Studio Tunnel Service`
    /// </summary>
    /// <remarks>
    /// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
    /// in the PROD service environment.
    /// </remarks>
    internal const string ProdFirstPartyAppId = "46da2f7e-b5ef-422a-88d4-2a7f9de6a0b2";

    /// <summary>
    /// First-party app ID: `Visual Studio Tunnel Service - Test`
    /// </summary>
    /// <remarks>
    /// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
    /// in the PPE service environments.
    /// </remarks>
    internal const string PpeFirstPartyAppId = "54c45752-bacd-424a-b928-652f3eca2b18";

    /// <summary>
    /// First-party app ID: `DEV-VSTunnels`
    /// </summary>
    /// <remarks>
    /// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
    /// in the DEV service environment
    /// </remarks>
    internal const string DevFirstPartyAppId = "9c63851a-ba2b-40a5-94bd-890be43b9284";

    /// <summary>
    /// Third-party app ID: `tunnels-prod-app-sp`
    /// </summary>
    /// <remarks>
    /// Used for authenticating internal AAD service principals in the AME tenant,
    /// in the PROD service environment.
    /// </remarks>
    internal const string ProdThirdPartyAppId = "ce65d243-a913-4cae-a7dd-cb52e9f77647";

    /// <summary>
    /// Third-party app ID: `tunnels-ppe-app-sp`
    /// </summary>
    /// <remarks>
    /// Used for authenticating internal AAD service principals in the AME tenant,
    /// in the PPE service environment.
    /// </remarks>
    internal const string PpeThirdPartyAppId = "544167a6-f431-4518-aac6-2fd50071928e";

    /// <summary>
    /// Third-party app ID: `tunnels-dev-app-sp`
    /// </summary>
    /// <remarks>
    /// Used for authenticating internal AAD service principals in the corp tenant (not AME!),
    /// in the DEV service environment.
    /// </remarks>
    internal const string DevThirdPartyAppId = "a118c979-0249-44bb-8f95-eb0457127aeb";

    /// <summary>
    /// GitHub App Client ID for 'Visual Studio Tunnel Service'
    /// </summary>
    /// <remarks>
    /// Used by client apps that authenticate tunnel users with GitHub, in the PROD
    /// service environment.
    /// </remarks>
    internal const string ProdGitHubAppClientId = "Iv1.e7b89e013f801f03";

    /// <summary>
    /// GitHub App Client ID for 'Visual Studio Tunnel Service - Test'
    /// </summary>
    /// <remarks>
    /// Used by client apps that authenticate tunnel users with GitHub, in the PPE and DEV
    /// service environments.
    /// </remarks>
    internal const string NonProdGitHubAppClientId = "Iv1.b231c327f1eaa229";

    private TunnelServiceProperties(
        string serviceUri,
        string serviceAppId,
        string serviceInternalAppId,
        string gitHubAppClientId)
    {
        ServiceUri = serviceUri;
        ServiceAppId = serviceAppId;
        ServiceInternalAppId = serviceInternalAppId;
        GitHubAppClientId = gitHubAppClientId;
    }

    /// <summary>
    /// Gets production service properties.
    /// </summary>
    public static TunnelServiceProperties Production { get; } = new TunnelServiceProperties(
        $"https://{ProdDnsName}/",
        ProdFirstPartyAppId,
        ProdThirdPartyAppId,
        ProdGitHubAppClientId);

    /// <summary>
    /// Gets properties for the service in the staging environment (PPE).
    /// </summary>
    public static TunnelServiceProperties Staging { get; } = new TunnelServiceProperties(
        $"https://{PpeDnsName}/",
        PpeFirstPartyAppId,
        PpeThirdPartyAppId,
        NonProdGitHubAppClientId);

    /// <summary>
    /// Gets properties for the service in the development environment.
    /// </summary>
    public static TunnelServiceProperties Development { get; } = new TunnelServiceProperties(
        $"https://{DevDnsName}/",
        DevFirstPartyAppId,
        DevThirdPartyAppId,
        NonProdGitHubAppClientId);

    /// <summary>
    /// Gets properties for the service in the specified environment.
    /// </summary>
    /// <param name="environmentName">A service environment string from
    /// `Microsoft.Extensions.Hosting.Abstractions.Environments`.</param>
    /// <returns>Service properties.</returns>
    public static TunnelServiceProperties Environment(string environmentName)
    {
        if (string.IsNullOrEmpty(environmentName))
        {
            throw new ArgumentNullException(nameof(environmentName));
        }

        return environmentName.ToLowerInvariant() switch
        {
            "prod" or "production" => TunnelServiceProperties.Production,
            "ppe" or "preprod" or "staging" => TunnelServiceProperties.Staging,
            "dev" or "development" => TunnelServiceProperties.Development,
            _ => throw new ArgumentException($"Invalid service environment: {environmentName}"),
        };
    }

    /// <summary>
    /// Gets the base URI of the service.
    /// </summary>
    public string ServiceUri { get; }

    /// <summary>
    /// Gets the public AAD AppId for the service.
    /// </summary>
    /// <remarks>
    /// Clients specify this AppId as the audience property when authenticating to the service.
    /// </remarks>
    public string ServiceAppId { get; }

    /// <summary>
    /// Gets the internal AAD AppId for the service.
    /// </summary>
    /// <remarks>
    /// Other internal services specify this AppId as the audience property when authenticating
    /// to the tunnel service. Production services must be in the AME tenant to use this appid.
    /// </remarks>
    public string ServiceInternalAppId { get; }

    /// <summary>
    /// Gets the client ID for the service's GitHub app.
    /// </summary>
    /// <remarks>
    /// Clients apps that authenticate tunnel users with GitHub specify this as the client ID
    /// when requesting a user token.
    /// </remarks>
    public string GitHubAppClientId { get; }
}
