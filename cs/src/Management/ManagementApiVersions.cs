// <copyright file="ManagementApiVersions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Allowed Api Versions for Management API.
/// </summary>
public enum ManagementApiVersions
{
    /// <summary>
    /// Value for api version 2023-09-27-preview.
    /// </summary>
    Version20230927Preview,
}


/// <summary>
/// Version extensions for ManagementApiVersions enum.
/// </summary>
public static class VersionExtensions
{
    /// <summary>
    /// Get string version from enum
    /// </summary>
    /// <param name="version">Enum value</param>
    /// <returns>String representaion of version</returns>
    public static string ToVersionString(this ManagementApiVersions version)
    {
        switch (version)
        {
            case ManagementApiVersions.Version20230927Preview:   return "2023-09-27-preview";
            default:                                             return "2023-09-27-preview";
        }
    }
}