// <copyright file="UnauthorizedAccessExceptionExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;

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
    public static string[]? GetAuthenticationSchemes(this UnauthorizedAccessException ex)
    {
        Requires.NotNull(ex, nameof(ex));

        lock (ex.Data)
        {
            var authenticationSchemes = ex.Data[AuthenticationSchemesKey] as string[];
            return authenticationSchemes;
        }
    }

    /// <summary>
    /// Sets the list of schemes that may be used to authenticate, when an
    /// <see cref="UnauthorizedAccessException" /> was thrown for an unauthenticated request.
    /// </summary>
    public static void SetAuthenticationSchemes(
        this UnauthorizedAccessException ex,
        string[]? authenticationSchemes)
    {
        lock (ex.Data)
        {
            ex.Data[AuthenticationSchemesKey] = authenticationSchemes;
        }
    }
}
