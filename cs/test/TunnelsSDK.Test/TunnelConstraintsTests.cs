using System;
using Microsoft.DevTunnels.Contracts;
using Xunit;

namespace Microsoft.DevTunnels.Test;

using static TunnelConstraints;

/// <summary>
/// Tests the validate <see cref="TunnelConstraints"/>.
/// </summary>
public class TunnelConstraintsTests
{
    [Theory]
    [InlineData("01234567")]
    [InlineData("89bcdfgh")]
    [InlineData("stvwxzzz")]
    public void IsValidTunnelId_Valid(string tunnelId)
    {
        Assert.True(IsValidOldTunnelId(tunnelId));
        ValidateOldTunnelId(tunnelId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000000")]   // 7 chars - shorter
    [InlineData("000000000")] // 9 chars - longer
    [InlineData("000-0000")]  // 8 chars with invalid char ('-')
    public void IsValidTunnelId_NotValid(string tunnelId)
    {
        Assert.False(IsValidOldTunnelId(tunnelId));
        if (tunnelId == null)
        {
            Assert.Throws<ArgumentNullException>(() => ValidateOldTunnelId(tunnelId));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => ValidateOldTunnelId(tunnelId));
        }
    }

    [Theory]
    [InlineData("012345679")]
    [InlineData("89bcdfg")]
    [InlineData("test")]
    [InlineData("aaa")]
    [InlineData("bcd-ghjk")]
    [InlineData("jfullerton44-name-with-special-char--jrw9q5vrfjpwx")]
    [InlineData("012345678901234567890123456789012345678901234567890123456789")]
    public void IsValidTunnelName_Valid(string tunnelName)
    {
        Assert.True(IsValidTunnelName(tunnelName));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890")]
    [InlineData("89bcdfgh")]
    [InlineData("stvwxzzz")]
    [InlineData("stv wxzzz")]
    [InlineData("aaaa-bbb-ccc!!!")]
    public void IsValidTunnelName_NotValid(string tunnelName)
    {
        Assert.False(IsValidTunnelName(tunnelName));
    }

    [Theory]
    [InlineData("012345679")]
    [InlineData("89bcdfg")]
    [InlineData("test")]
    [InlineData("aa=a")]
    [InlineData("bcd-ghjk")]
    [InlineData("jfullerton44-name-with-special-char--jrw9q5vrfjpwx")]
    [InlineData("codespace_id=2e0ffd29-b8fa-42bd-94bc-e764a8381ca9")]
    public void IsValidTunnelTag_Valid(string tag)
    {
        Assert.True(IsValidTag(tag));
    }

    [Theory]
    [InlineData("a ")]
    [InlineData("89bcdfg,h")]
    [InlineData("stv wxzzz")]
    [InlineData("aaaa-bbb-ccc!!!")]
    public void IsValidTunnelTag_NotValid(string tag)
    {
        Assert.False(IsValidTag(tag));
    }
}
