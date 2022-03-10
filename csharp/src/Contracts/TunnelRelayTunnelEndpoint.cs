// <copyright file="TunnelRelayTunnelEndpoint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Parameters for connecting to a tunnel via the tunnel service's built-in relay function.
    /// </summary>
    public class TunnelRelayTunnelEndpoint : TunnelEndpoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelRelayTunnelEndpoint"/> class.
        /// </summary>
        public TunnelRelayTunnelEndpoint()
        {
            ConnectionMode = TunnelConnectionMode.TunnelRelay;
        }

        /// <summary>
        /// Gets or sets the host URI.
        /// </summary>
        public string? HostRelayUri { get; set; }

        /// <summary>
        /// Gets or sets the client URI.
        /// </summary>
        public string? ClientRelayUri { get; set; }

        /// <summary>
        /// Gets or sets an array of public keys, which can be used by clients to authenticate.
        /// the host.
        /// </summary>
        public string[]? HostPublicKeys { get; set; }
    }
}
