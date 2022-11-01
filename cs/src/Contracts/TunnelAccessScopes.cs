// <copyright file="TunnelAccessScopes.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Defines scopes for tunnel access tokens.
/// </summary>
/// <remarks>
/// A tunnel access token with one or more of these scopes typically also has cluster ID and
/// tunnel ID claims that limit the access scope to a specific tunnel, and may also have one
/// or more port claims that further limit the access to particular ports of the tunnel.
/// </remarks>
public static class TunnelAccessScopes
{
    internal const int MaxLength = 20;

    /// <summary>
    /// Allows creating tunnels. This scope is valid only in policies at the global, domain,
    /// or organization level; it is not relevant to an already-created tunnel or tunnel port.
    /// (Creation of ports requires "manage" or "host" access to the tunnel.)
    /// </summary>
    public const string Create = "create";

    /// <summary>
    /// Allows management operations on tunnels and tunnel ports.
    /// </summary>
    public const string Manage = "manage";

    /// <summary>
    /// Allows management operations on all ports of a tunnel, but does not allow updating any
    /// other tunnel properties or deleting the tunnel.
    /// </summary>
    public const string ManagePorts = "manage:ports";

    /// <summary>
    /// Allows accepting connections on tunnels as a host. Includes access to update tunnel
    /// endpoints and ports.
    /// </summary>
    public const string Host = "host";

    /// <summary>
    /// Allows inspecting tunnel connection activity and data.
    /// </summary>
    public const string Inspect = "inspect";

    /// <summary>
    /// Allows connecting to tunnels or ports as a client.
    /// </summary>
    public const string Connect = "connect";

    /// <summary>
    /// Array of all access scopes. Primarily used for validation.
    /// </summary>
    public static readonly string[] All = new[]
    {
        Create,
        Manage,
        ManagePorts,
        Host,
        Inspect,
        Connect,
    };
}
