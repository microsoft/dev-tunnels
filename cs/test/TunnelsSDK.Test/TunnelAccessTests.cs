using System;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Xunit;

namespace Microsoft.DevTunnels.Test;

/// <summary>
/// Tests that validate tunnel access control APIs.
/// </summary>
public class TunnelAccessTests
{
    [Fact]
    public void IsAnonymousAllowed()
    {
        var accessControl = new TunnelAccessControl
        {
            Entries = new[]
            {
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Anonymous,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                },
            },
        };

        Assert.True(accessControl.IsAnonymousAllowed(
            TunnelAccessScopes.Connect));
        Assert.Null(accessControl.IsAllowed(
            TunnelAccessControlEntryType.Users, "test", TunnelAccessScopes.Connect));
    }

    [Fact]
    public void IsUserAllowed()
    {
        var accessControl = new TunnelAccessControl
        {
            Entries = new[]
            {
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Users,
                    Provider = TunnelAccessControlEntry.Providers.Microsoft,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                    Subjects = new[] { "test" },
                },
            },
        };

        Assert.True(accessControl.IsAllowed(
            TunnelAccessControlEntryType.Users, "test", TunnelAccessScopes.Connect));
        Assert.Null(accessControl.IsAnonymousAllowed(TunnelAccessScopes.Connect));
    }

    [Fact]
    public void IsDeniedAnonymousAllowed()
    {
        var accessControl = new TunnelAccessControl
        {
            Entries = new[]
            {
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Anonymous,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                    IsDeny = true,
                    IsInherited = true,
                },
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Anonymous,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                },
            },
        };

        Assert.False(accessControl.IsAnonymousAllowed(TunnelAccessScopes.Connect));
    }

    [Fact]
    public void IsDeniedUserAllowed()
    {
        var accessControl = new TunnelAccessControl
        {
            Entries = new[]
            {
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Users,
                    Provider = TunnelAccessControlEntry.Providers.Microsoft,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                    Subjects = new[] { "test" },
                    IsDeny = true,
                    IsInherited = true,
                },
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Users,
                    Provider = TunnelAccessControlEntry.Providers.Microsoft,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                    Subjects = new[] { "test" },
                },
            },
        };

        Assert.False(accessControl.IsAllowed(
            TunnelAccessControlEntryType.Users, "test", TunnelAccessScopes.Connect));
        Assert.Null(accessControl.IsAnonymousAllowed(TunnelAccessScopes.Connect));
    }

    [Fact]
    public void IsInverseDeniedOrgAllowed()
    {
        var accessControl = new TunnelAccessControl
        {
            Entries = new[]
            {
                // Deny access to anyone who is NOT in the org.
                new TunnelAccessControlEntry
                {
                    Type = TunnelAccessControlEntryType.Organizations,
                    Provider = TunnelAccessControlEntry.Providers.Microsoft,
                    Scopes = new[] { TunnelAccessScopes.Connect },
                    Subjects = new[] { "test" },
                    IsDeny = true,
                    IsInverse = true,
                },
            },
        };

        Assert.False(accessControl.IsAllowed(
            TunnelAccessControlEntryType.Organizations, "test2", TunnelAccessScopes.Connect));
        Assert.Null(accessControl.IsAllowed(
            TunnelAccessControlEntryType.Organizations, "test", TunnelAccessScopes.Connect));
        Assert.Null(accessControl.IsAnonymousAllowed(TunnelAccessScopes.Connect));
    }
}
