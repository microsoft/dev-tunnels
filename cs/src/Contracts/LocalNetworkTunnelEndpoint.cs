// <copyright file="LocalNetworkTunnelEndpoint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Parameters for connecting to a tunnel via a local network connection.
/// </summary>
/// <remarks>
/// While a direct connection is technically not "tunneling", tunnel hosts may accept
/// connections via the local network as an optional more-efficient alternative to a relay.
/// </remarks>
public class LocalNetworkTunnelEndpoint : TunnelEndpoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalNetworkTunnelEndpoint"/> class.
    /// </summary>
    public LocalNetworkTunnelEndpoint()
    {
        ConnectionMode = TunnelConnectionMode.LocalNetwork;
    }

    /// <summary>
    /// Gets or sets a list of IP endpoints where the host may accept connections.
    /// </summary>
    /// <remarks>
    /// A host may accept connections on multiple IP endpoints simultaneously if there
    /// are multiple network interfaces on the host system and/or if the host supports both
    /// IPv4 and IPv6.
    ///
    /// Each item in the list is a URI consisting of a scheme (which gives an indication
    /// of the network connection protocol), an IP address (IPv4 or IPv6) and a port number.
    /// The URIs do not typically include any paths, because the connection is not normally
    /// HTTP-based.
    /// </remarks>
    public string[] HostEndpoints { get; set; } = null!;
}
