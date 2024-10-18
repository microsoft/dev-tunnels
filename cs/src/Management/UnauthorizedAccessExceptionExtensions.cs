// <copyright file="UnauthorizedAccessExceptionExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Extension methods for <see cref="UnauthorizedAccessException"/>.
/// </summary>
public static class UnauthorizedAccessExceptionExtensions
{
    private const string AuthenticationSchemesKey = "AuthenticationSchemes";
    private const string EnterprisePolicyRequirementsKey = "EnterprisePolicyRequirements";

    /// <summary>
    /// Gets the list of schemes that may be used to authenticate, when an
    /// <see cref="UnauthorizedAccessException" /> was thrown for an unauthenticated request.
    /// </summary>
    public static IEnumerable<AuthenticationHeaderValue> GetAuthenticationSchemes(
        this UnauthorizedAccessException ex)
    {
        Requires.NotNull(ex, nameof(ex));

        lock (ex.Data)
        {
            var authenticationSchemes = ex.Data[AuthenticationSchemesKey] as string[];
            return authenticationSchemes?
                .Select((s) => AuthenticationHeaderValue.TryParse(s, out var value) ? value : null!)
                .Where((s) => s != null)
                .ToArray() ?? Enumerable.Empty<AuthenticationHeaderValue>();
        }
    }

    /// <summary>
    /// Sets the list of schemes that may be used to authenticate, when an
    /// <see cref="UnauthorizedAccessException" /> was thrown for an unauthenticated request.
    /// </summary>
    public static void SetAuthenticationSchemes(
        this UnauthorizedAccessException ex,
        IEnumerable<AuthenticationHeaderValue>? authenticationSchemes)
    {
        SetAuthenticationSchemes(ex, authenticationSchemes?
            .Select((s) => s?.ToString()!)
            .Where((s) => s != null));
    }

    internal static void SetAuthenticationSchemes(
        this UnauthorizedAccessException ex,
        IEnumerable<string>? authenticationSchemes)
    {
        lock (ex.Data)
        {
            ex.Data[AuthenticationSchemesKey] = authenticationSchemes?.ToArray();
        }
    }

    /// <summary>
    /// Gets the list of enterprise policy requirements that caused the
    /// <see cref="UnauthorizedAccessException" />.
    /// </summary>
    /// <remarks>
    /// Each item is a non-localized string policy requirement name, such as:
    ///   "DisableAnonymousAccessRequirement",
    ///   "DisableDevTunnelsRequirement",
    ///   "RestrictedTenantAccessRequirement"
    /// </remarks>
    public static IEnumerable<string> GetEnterprisePolicyRequirements(
        this UnauthorizedAccessException ex)
    {
        Requires.NotNull(ex, nameof(ex));

        lock (ex.Data)
        {
            return ex.Data[EnterprisePolicyRequirementsKey] as string[] ??
                Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Sets the list of enterprise policy requirements that caused the
    /// <see cref="UnauthorizedAccessException" />.
    /// </summary>
    public static void SetEnterprisePolicyRequirements(
        this UnauthorizedAccessException ex,
        IEnumerable<string>? enterprisePolicyRequirements)
    {
        lock (ex.Data)
        {
            ex.Data[EnterprisePolicyRequirementsKey] = enterprisePolicyRequirements?.ToArray();
        }
    }
}
