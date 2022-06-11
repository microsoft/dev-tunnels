// <copyright file="UnauthorizedAccessExceptionExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Extension methods for <see cref="UnauthorizedAccessException"/>.
/// </summary>
public static class UnauthorizedAccessExceptionExtensions
{
    private const string AuthenticationSchemesKey = "AuthenticationSchemes";

    /// <summary>
    /// Gets the list of schemes that may be used to authenticate, when an
    /// <see cref="UnauthorizedAccessException" /> was thrown for an unauthenticated request.
    /// </summary>
    public static IEnumerable<AuthenticationHeaderValue>? GetAuthenticationSchemes(
        this UnauthorizedAccessException ex)
    {
        Requires.NotNull(ex, nameof(ex));

        lock (ex.Data)
        {
            var authenticationSchemes = ex.Data[AuthenticationSchemesKey] as string[];
            return authenticationSchemes?
                .Select((s) => AuthenticationHeaderValue.TryParse(s, out var value) ? value : null!)
                .Where((s) => s != null)
                .ToArray();
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
        lock (ex.Data)
        {
            ex.Data[AuthenticationSchemesKey] = authenticationSchemes?
                .Select((s) => s?.ToString() !)
                .Where((s) => s != null)
                .ToArray();
        }
    }
}
