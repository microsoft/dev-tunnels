// <copyright file="TunnelAccessTokenProperties.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService;

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
    /// Gets or sets the token cluster ID claim.
    /// </summary>
    [JsonPropertyName(ClusterIdClaimName)]
    public string? ClusterId { get; set; }

    /// <summary>
    /// Gets or sets the token tunnel ID claim.
    /// </summary>
    [JsonPropertyName(TunnelIdClaimName)]
    public string? TunnelId { get; set; }

    /// <summary>
    /// Gets or sets the token tunnel port claim.
    /// </summary>
    [JsonPropertyName(TunnelPortClaimName)]
    public int? TunnelPort { get; set; }

    /// <summary>
    /// Gets or sets the token scope claim, as a space-separated list of scope names.
    /// </summary>
    [JsonPropertyName(ScopeClaimName)]
    public string Scope { get; set; } = null!;

    /// <summary>
    /// Gets the token scopes as a string array.
    /// </summary>
    [JsonIgnore]
    public string[]? Scopes
    {
        get
        {
            if (string.IsNullOrEmpty(Scope))
            {
                return null;
            }

            return Scope.Split(' ');
        }
    }

    /// <summary>
    /// Gets or sets the token issuer URI.
    /// </summary>
    [JsonPropertyName(IssuerClaimName)]
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the token expiration timestamp.
    /// </summary>
    [JsonPropertyName(ExpirationClaimName)]
    public int ExpirationTimestamp { get; set; }

    /// <summary>
    /// Gets the token expiration as a <see cref="System.DateTime" />.
    /// </summary>
    [JsonIgnore]
    public DateTime? Expiration
    {
        get
        {
            if (ExpirationTimestamp == 0)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(ExpirationTimestamp).UtcDateTime;
        }
    }

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

        if (TunnelPort != null)
        {
            if (s.Length > 0) s.Append(", ");
            s.Append("port=");
            s.Append(TunnelPort);
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
            var tokenProperties = JsonSerializer.Deserialize<TunnelAccessTokenProperties>(tokenBodyJson)!;
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
