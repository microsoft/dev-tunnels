// <copyright file="TunnelPlanTokenProperties.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Supports parsing tunnelPlan token JWT properties to allow for some pre-validation
/// and diagnostics.
/// </summary>
public class TunnelPlanTokenProperties
{
    private const string ClusterIdClaimName = "clusterId";
    private const string IssuerClaimName = "iss";
    private const string ExpirationClaimName = "exp";
    private const string UserEmailClaimName = "userEmail";
    private const string TunnelPlanIdClaimName = "tunnelPlanId";
    private const string SubscriptionIdClaimName = "subscriptionId";
    private const string ScopeClaimName = "scp";


    /// <summary>
    /// Gets the token cluster ID claim.
    /// </summary>
    public string? ClusterId { get; private set; }

    /// <summary>
    /// Gets the subscription ID claim.
    /// </summary>
    public string? SubscriptionId { get; private set; }

    /// <summary>
    /// Gets the token user email claim.
    /// </summary>
    public string? UserEmail { get; private set; }

    /// <summary>
    /// Gets the token scopes claim.
    /// </summary>
    public string[]? Scopes { get; private set; }

    /// <summary>
    /// Gets the token issuer URI.
    /// </summary>
    public string? Issuer { get; private set; }

    /// <summary>
    /// Gets the token expiration.
    /// </summary>
    [JsonIgnore]
    public DateTime? Expiration { get; private set; }

    /// <summary>
    /// Gets the token tunnel plan id claim.
    /// </summary>
    public string? TunnelPlanId { get; private set; }

    /// <summary>
    /// Checks if the tunnel access token expiration claim is in the past.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The token is expired.</exception>
    /// <remarks>Note this does not throw if the token is an invalid format.</remarks>
    public static void ValidateTokenExpiration(string token)
    {
        var t = TryParse(token);
        if (t?.Expiration <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("The access token is expired: " + t);
        }
    }

    /// <summary>
    /// Gets token representation for tracing.
    /// </summary>
    public static string GetTokenTrace(string? token)
    {
        if (token == null)
        {
            return "<null>";
        }

        if (token == string.Empty)
        {
            return "<empty>";
        }

        return TryParse(token) is TunnelPlanTokenProperties t ? $"<JWT: {t}>" : "<token>";
    }

    /// <summary>
    /// Attempts to parse a tunnel access token (JWT). This does NOT validate the token
    /// signature or any claims.
    /// </summary>
    /// <returns>The parsed token properties, or null if the token is an invalid format.</returns>
    /// <remarks>
    /// Applications generally should not attempt to interpret or rely on any token properties
    /// other than <see cref="Expiration" />, because the service may change or omit those claims
    /// in the future. Other claims are exposed here only for diagnostic purposes.
    /// </remarks>
    public static TunnelPlanTokenProperties? TryParse(string token)
    {
        Requires.NotNullOrEmpty(token, nameof(token));

        // JWTs are encoded in 3 parts: header, body, and signature.
        var tokenParts = token.Split('.');
        if (tokenParts.Length != 3)
        {
            return null;
        }

        var tokenBodyJson = Base64UrlDecode(tokenParts[1]);
        if (tokenBodyJson == null)
        {
            return null;
        }

        try
        {
            var tokenElement = JsonSerializer.Deserialize<JsonElement>(tokenBodyJson)!;
            var tokenProperties = new TunnelPlanTokenProperties();

            if (tokenElement.TryGetProperty(ClusterIdClaimName, out var clusterIdElement))
            {
                tokenProperties.ClusterId = clusterIdElement.GetString();
            }

            if (tokenElement.TryGetProperty(SubscriptionIdClaimName, out var subscriptionId))
            {
                tokenProperties.SubscriptionId = subscriptionId.GetString();
            }

            if (tokenElement.TryGetProperty(TunnelPlanIdClaimName, out var tunnelPlanId))
            {
                tokenProperties.TunnelPlanId = tunnelPlanId.GetString();
            }

            if (tokenElement.TryGetProperty(UserEmailClaimName, out var userEmail))
            {
                tokenProperties.UserEmail = userEmail.GetString();
            }

            if (tokenElement.TryGetProperty(ScopeClaimName, out var scopeElement))
            {
                var scopes = scopeElement.GetString();
                tokenProperties.Scopes = string.IsNullOrEmpty(scopes) ? null : scopes.Split(' ');
            }

            if (tokenElement.TryGetProperty(IssuerClaimName, out var issuerElement))
            {
                tokenProperties.Issuer = issuerElement.GetString();
            }

            if (tokenElement.TryGetProperty(ExpirationClaimName, out var expirationElement) &&
                expirationElement.ValueKind == JsonValueKind.Number)
            {
                var exp = expirationElement.GetInt64();
                tokenProperties.Expiration = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }

            return tokenProperties;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Base64UrlDecode(string encodedString)
    {
        // Convert from base64url encoding to base64 encoding: replace chars and add padding.
        encodedString = encodedString.Replace('-', '+').Replace('_', '/');
        encodedString += new string('=', 3 - ((encodedString.Length - 1) % 4));

        try
        {
            var bytes = Convert.FromBase64String(encodedString);
            var result = Encoding.UTF8.GetString(bytes);
            return result;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
