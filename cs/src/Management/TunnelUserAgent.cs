// <copyright file="TunnelUserAgent.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Net.Http.Headers;
using System.Reflection;
using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
    /// Properties include os details and windows
    /// partner id if applicable
    /// </summary>
    /// <returns>Product info header values with
    /// machine properties.</returns>
    public static ProductInfoHeaderValue? GetMachineHeaders()
    {
        var headerComments = new List<string>();
        var os = RuntimeInformation.OSDescription;
        headerComments.Add("OS" + ":" + os.ToString());

#if NET6_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            string registryKeyName = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows365";
            string valueName = "PartnerId";

            object? retrievedValue = Registry.GetValue(registryKeyName, valueName, RegistryOptions.None);

            if (retrievedValue != null)
            {
                headerComments.Add("Windows-Partner-Id" + ":" + retrievedValue.ToString());
            }
        }
#endif
        if (headerComments.Any())
        {
            string headerCommentValue = "(" + string.Join("; ", headerComments) + ")";
            return new ProductInfoHeaderValue(headerCommentValue);
        }
        else
        {
            return null;
        }
    }
}
