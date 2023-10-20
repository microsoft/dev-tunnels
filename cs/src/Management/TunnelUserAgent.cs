// <copyright file="TunnelUserAgent.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Net.Http.Headers;
using System.Reflection;
using System;
using Microsoft.Win32;
using System.Collections.Generic;

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

    /// <summary>
    /// Gets machine properties and adds them to the user agent.
    /// Properties include os, os version, and windows
    /// partner id if applicable
    /// </summary>
    /// <returns>List of product info header values with
    /// machine properties.</returns>
    public static List<ProductInfoHeaderValue> GetMachineHeaders()
    {
        var windowsHeaderValues = new List<ProductInfoHeaderValue>();
        var os = Environment.OSVersion.Platform;
        windowsHeaderValues.Add(ProductInfoHeaderValue.Parse("OS" + "/" + os.ToString()));
        var version = Environment.OSVersion.Version;
        windowsHeaderValues.Add(ProductInfoHeaderValue.Parse("OS-Version" + "/" + version.ToString()));

#if NET6_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            string registryKeyName = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows365";
            string valueName = "PartnerId";

            object? retrievedValue = Registry.GetValue(registryKeyName, valueName, RegistryOptions.None);

            if (retrievedValue != null)
            {
                var id = "Windows-Partner-Id" + "/" + retrievedValue.ToString();
                if (string.IsNullOrEmpty(id) == false)
                {
                    windowsHeaderValues.Add(ProductInfoHeaderValue.Parse(id));
                }
            }
        }
#endif
        return windowsHeaderValues;
    }
}
