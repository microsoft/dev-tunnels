// <copyright file="TunnelProtocol.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Defines possible values for the protocol of a <see cref="TunnelPort"/>.
/// </summary>
public static class TunnelProtocol
{
    internal const int MaxLength = 10;

    /// <summary>
    /// The protocol is automatically detected. (TODO: Define detection semantics.)
    /// </summary>
    public const string Auto = "auto";

    /// <summary>
    /// Unknown TCP protocol.
    /// </summary>
    public const string Tcp = "tcp";

    /// <summary>
    /// Unknown UDP protocol.
    /// </summary>
    public const string Udp = "udp";

    /// <summary>
    /// SSH protocol.
    /// </summary>
    public const string Ssh = "ssh";

    /// <summary>
    /// Remote desktop protocol.
    /// </summary>
    public const string Rdp = "rdp";

    /// <summary>
    /// HTTP protocol.
    /// </summary>
    public const string Http = "http";

    /// <summary>
    /// HTTPS protocol.
    /// </summary>
    public const string Https = "https";
}
