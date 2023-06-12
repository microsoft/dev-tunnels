using System.Text;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Xunit;
using static System.Formats.Asn1.AsnWriter;

namespace Microsoft.DevTunnels.Test;

public class TunnelExtensionsTests
{
    private static Tunnel Tunnel { get; } = new Tunnel
    {
        AccessTokens = new Dictionary<string, string>
        {
            ["scope1"] = "token1",
            ["scope2 scope3  scope4"] = "token2",
            [" scope5"] = "token3",
            ["scope6 "] = "token4",
            ["scope3"] = "token5",
            ["scope7"] = "",
            ["scope8 scope9"] = null,
        }
    };

    [Fact]
    public void TryGetAccessToken_NullTunnel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((Tunnel)null).TryGetAccessToken("scope", out var _));

    [Fact]
    public void TryGetAccessToken_NullScope_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Tunnel.TryGetAccessToken(null, out var _));

    [Fact]
    public void TryGetAccessToken_EmptyScope_Throws() =>
        Assert.Throws<ArgumentException>(() => Tunnel.TryGetAccessToken(string.Empty, out var _));

    [Fact]
    public void TryGetAccessToken_NullAccessTokens() =>
        Assert.False(new Tunnel().TryGetAccessToken("scope", out var _));

    [Fact]
    public void TryGetValidAccessToken_NullTunnel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((Tunnel)null).TryGetValidAccessToken("scope", out var _));

    [Fact]
    public void TryGetValidAccessToken_NullScope_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Tunnel.TryGetValidAccessToken(null, out var _));

    [Fact]
    public void TryGetValidAccessToken_EmptyScope_Throws() =>
        Assert.Throws<ArgumentException>(() => Tunnel.TryGetValidAccessToken(string.Empty, out var _));

    [Fact]
    public void TryGetValidAccessToken_NullAccessTokens() =>
        Assert.False(new Tunnel().TryGetValidAccessToken("scope", out var _));

    [Theory]
    [InlineData("scope1", "token1")]
    [InlineData("scope2", "token2")]
    [InlineData("scope3", "token2")]
    [InlineData("scope4", "token2")]
    [InlineData("scope5", "token3")]
    [InlineData("scope6", "token4")]
    public void TryGetAccessToken(string scope, string expectedToken)
    {
        Assert.True(Tunnel.TryGetAccessToken(scope, out var accessToken));
        Assert.Equal(expectedToken, accessToken);

        // All tokens in the tunnel are not valid JWT, so validation for expiration doesn't trip.
        Assert.True(Tunnel.TryGetValidAccessToken(scope, out accessToken));
        Assert.Equal(expectedToken, accessToken);
    }

    [Theory]
    [InlineData("scope2 scope3")]
    [InlineData("token1")]
    [InlineData("scope7")]
    [InlineData("scope8")]
    [InlineData("scope9")]
    public void TryGetAccessTokenMissingScope(string scope)
    {
        Assert.False(Tunnel.TryGetAccessToken(scope, out var accessToken));
        Assert.Null(accessToken);

        // All tokens in the tunnel are not valid JWT, so validation for expiration doesn't trip.
        Assert.False(Tunnel.TryGetValidAccessToken(scope, out accessToken));
        Assert.Null(accessToken);
    }

    [Fact]
    public void TryGetValidAccessTokenNotExipred()
    {
        var token = GetToken(isExpired: false);
        var tunnel = new Tunnel
        {
            AccessTokens = new Dictionary<string, string>
            {
                ["scope"] = token,
            },
        };

        Assert.True(tunnel.TryGetValidAccessToken("scope", out var accessToken));
        Assert.Equal(token, accessToken);
    }

    [Fact]
    public void TryGetValidAccessTokenExipred()
    {
        var token = GetToken(isExpired: true);
        var tunnel = new Tunnel
        {
            AccessTokens = new Dictionary<string, string>
            {
                ["scope"] = token,
            },
        };

        string accessToken = string.Empty;
        Assert.Throws<UnauthorizedAccessException>(() => tunnel.TryGetValidAccessToken("scope", out accessToken));
        Assert.Null(accessToken);
    }

    private static string GetToken(bool isExpired)
    {
        var exp = DateTimeOffset.UtcNow + (isExpired ? -TimeSpan.FromHours(1) : TimeSpan.FromHours(1));
        var claims = $"{{ \"exp\": {exp.ToUnixTimeSeconds():D} }}";
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claims))
            .TrimEnd('=')
            .Replace('/', '_')
            .Replace('+', '-');
        return $"header.{payload}.signature";
    }
}
