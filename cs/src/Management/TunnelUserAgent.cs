// <copyright file="TunnelUserAgent.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// User Agent helper for <see cref="TunnelManagementClient.UserAgents"/>.
/// </summary>
public static class TunnelUserAgent
{
    /// <summary>
    /// Get user agent <see cref="ProductInfoHeaderValue"/> from <paramref name="assembly"/>,
    /// using <paramref name="productName"/> or <see cref="AssemblyName.Name"/> as the product name,
    /// and <see cref="AssemblyInformationalVersionAttribute.InformationalVersion"/> or
    /// <see cref="AssemblyName.Version"/> as product version.
    /// </summary>
    /// <param name="assembly">Optional assembly to get the version and product name from.</param>
    /// <param name="productName">Optional product name.</param>
    /// <returns>Product info header value or null.</returns>
    public static ProductInfoHeaderValue? GetUserAgent(Assembly? assembly, string? productName = null)
    {
        if (productName == null)
        {
            if (assembly != null)
            {
                productName = assembly.GetName().Name?.Replace('.', '-');
            }

            if (productName == null)
            {
                return null;
            }
        }

        string? productVersion = null;
        if (assembly != null)
        {
            productVersion =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                assembly.GetName()?.Version?.ToString();
        }

        return new ProductInfoHeaderValue(productName, productVersion ?? "unknown");
    }
}
