// <copyright file="TunnelExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Extension methods for working with <see cref="Tunnel"/> objects.
/// </summary>
public static class TunnelExtensions
{
    /// <summary>
    /// Try to get an access token from <paramref name="tunnel"/> for <paramref name="accessTokenScope"/>.
    /// </summary>
    /// <remarks>
    /// The tokens are searched in <c>Tunnel.AccessTokens</c> dictionary where each
    /// key may be either a single scope or space-delimited list of scopes.
    /// </remarks>
    /// <param name="tunnel">The tunnel to get the access token from.</param>
    /// <param name="accessTokenScope">Access token scope to get the token for.</param>
    /// <param name="accessToken">If non-null and non-empty token is found, the token value. <c>null</c> if not found.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="tunnel"/> has non-null and non-empty an access token for <paramref name="accessTokenScope"/>;
    /// <c>false</c> if <paramref name="tunnel"/> has no access token for <paramref name="accessTokenScope"/> or the token is null or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnel"/> or <paramref name="accessTokenScope"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="accessTokenScope"/> is empty.</exception>
    public static bool TryGetAccessToken(this Tunnel tunnel, string accessTokenScope, [NotNullWhen(true)] out string? accessToken)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        Requires.NotNullOrEmpty(accessTokenScope, nameof(accessTokenScope));

        if (tunnel.AccessTokens?.Count > 0)
        {
            var scope = accessTokenScope.AsSpan();
            foreach (var (key, value) in tunnel.AccessTokens)
            {
                // Each key may be either a single scope or space-delimited list of scopes.
                var index = 0;
                while (index < key?.Length)
                {
                    var spaceIndex = key.IndexOf(' ', index);
                    if (spaceIndex == -1)
                    {
                        spaceIndex = key.Length;
                    }

                    if (spaceIndex - index == scope.Length &&
                        key.AsSpan(index, scope.Length).SequenceEqual(scope))
                    {
                        if (string.IsNullOrEmpty(value))
                        {
                            accessToken = null;
                            return false;
                        }

                        accessToken = value;
                        return true;
                    }

                    index = spaceIndex + 1;
                }
            }
        }

        accessToken = null;
        return false;
    }

    /// <summary>
    /// Try to get a valid access token from <paramref name="tunnel"/> for <paramref name="accessTokenScope"/>.
    /// If the token is found and looks like JWT, it's validated for expiration.
    /// </summary>
    /// <remarks>
    /// The tokens are searched in <c>Tunnel.AccessTokens</c> dictionary where each
    /// key may be either a single scope or space-delimited list of scopes.
    /// The method only validates token expiration. It doesn't validate if the token is not JWT. It doesn't validate JWT signature or claims.
    /// Uses the client's system time for validation.
    /// </remarks>
    /// <param name="tunnel">The tunnel to get the access token from.</param>
    /// <param name="accessTokenScope">Access token scope to get the token for.</param>
    /// <param name="accessToken">If the token is found and it's valid, the token value. <c>null</c> if not found.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="tunnel"/> has a valid token for <paramref name="accessTokenScope"/>;
    /// <c>false</c> if <paramref name="tunnel"/> has no access token for <paramref name="accessTokenScope"/> or the token is null or empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">If <paramref name="tunnel"/> or <paramref name="accessTokenScope"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="accessTokenScope"/> is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">If the token for <paramref name="accessTokenScope"/> is expired.</exception>
    public static bool TryGetValidAccessToken(this Tunnel tunnel, string accessTokenScope, [NotNullWhen(true)] out string? accessToken)
    {
        Requires.NotNull(tunnel, nameof(tunnel));
        Requires.NotNullOrEmpty(accessTokenScope, nameof(accessTokenScope));

        accessToken = null;
        if (tunnel.TryGetAccessToken(accessTokenScope, out var result))
        {
            TunnelAccessTokenProperties.ValidateTokenExpiration(result);
            accessToken = result;
            return true;
        }

        return false;
    }
}
