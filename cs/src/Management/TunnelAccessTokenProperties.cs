// <copyright file="TunnelAccessTokenProperties.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Supports parsing tunnel access token JWT properties to allow for some pre-validation
/// and diagnostics.
/// </summary>
/// <remarks>
/// Applications generally should not attempt to interpret or rely on any token properties
/// other than <see cref="TunnelAccessTokenProperties.Expiration" />, because the service
/// may change or omit those claims in the future. Other claims are exposed here only for
/// diagnostic purposes.
/// </remarks>
public class TunnelAccessTokenProperties
{
    private const string ClusterIdClaimName = "clusterId";
    private const string TunnelIdClaimName = "tunnelId";
    private const string TunnelPortClaimName = "tunnelPort";
    private const string ScopeClaimName = "scp";
    private const string IssuerClaimName = "iss";
    private const string ExpirationClaimName = "exp";

    /// <summary>
    /// Gets the token cluster ID claim.
    /// </summary>
    public string? ClusterId { get; private set; }

    /// <summary>
    /// Gets the token tunnel ID claim.
    /// </summary>
    public string? TunnelId { get; private set; }

    /// <summary>
    /// Gets the token tunnel ports claim.
    /// </summary>
    public ushort[]? TunnelPorts { get; private set; }

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

    /// <inheritdoc/>
    public override string ToString()
    {
        var s = new StringBuilder();

        if (!string.IsNullOrEmpty(TunnelId))
        {
            s.Append("tunnel=");
            s.Append(TunnelId);

            if (!string.IsNullOrEmpty(ClusterId))
            {
                s.Append('.');
                s.Append(ClusterId);
            }
        }

        if (TunnelPorts != null)
        {
            if (s.Length > 0) s.Append(", ");
            if (TunnelPorts.Length == 1)
            {
                s.Append("port=");
                s.Append(TunnelPorts[0]);
            }
            else
            {
                s.AppendFormat("ports=[{0}]", string.Join(", ", TunnelPorts));
            }
        }

        var scopes = Scopes;
        if (scopes != null)
        {
            if (s.Length > 0) s.Append(", ");
            s.AppendFormat("scopes=[{0}]", string.Join(", ", scopes));
        }

        if (!string.IsNullOrEmpty(Issuer))
        {
            if (s.Length > 0) s.Append(", ");
            s.Append("issuer=");
            s.Append(Issuer);
        }

        var expiration = Expiration;
        if (expiration != null)
        {
            if (s.Length > 0) s.Append(", ");

            // Get the current date-time without fractional seconds.
            var nowTicks = DateTime.UtcNow.Ticks;
            var now = new DateTime(nowTicks - nowTicks % 10000000, DateTimeKind.Utc);

            var lifetime = expiration.Value >= now
                ? (expiration.Value - now).ToString() + " remaining"
                : (now - expiration.Value) + " ago";
            s.AppendFormat("expiration={0:s}Z ({1})", expiration.Value, lifetime);
        }

        return s.ToString();
    }

    /// <summary>
    /// Checks if the tunnel access token expiration claim is in the past.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The token is expired.</exception>
    /// <remarks>
    /// Note this does not throw if the token is an invalid format.
    /// Uses the client's system time for validation.
    /// </remarks>
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

        return TryParse(token) is TunnelAccessTokenProperties t ? $"<JWT: {t}>" : "<token>";
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
    public static TunnelAccessTokenProperties? TryParse(string token)
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
            var tokenProperties = new TunnelAccessTokenProperties();

            if (tokenElement.TryGetProperty(ClusterIdClaimName, out var clusterIdElement))
            {
                tokenProperties.ClusterId = clusterIdElement.GetString();
            }

            if (tokenElement.TryGetProperty(TunnelIdClaimName, out var tunnelIdElement))
            {
                tokenProperties.TunnelId = tunnelIdElement.GetString();
            }

            if (tokenElement.TryGetProperty(TunnelPortClaimName, out var tunnelPortElement))
            {
                // The port claim value may be a single port number or an array of ports.
                if (tunnelPortElement.ValueKind == JsonValueKind.Array)
                {
                    var array = new ushort[tunnelPortElement.GetArrayLength()];

                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = tunnelPortElement[i].GetUInt16();
                    }

                    tokenProperties.TunnelPorts = array;
                }
                else if (tunnelPortElement.ValueKind == JsonValueKind.Number)
                {
                    tokenProperties.TunnelPorts = new[] { tunnelPortElement.GetUInt16() };
                }
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
