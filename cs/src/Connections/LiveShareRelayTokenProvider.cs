// <copyright file="LiveShareRelayTokenProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Relay;
using Microsoft.VsSaaS.TunnelService.Contracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Provides Azure Relay access tokens by querying them via a tunnel management client.
    /// </summary>
    internal class LiveShareRelayTokenProvider : TokenProvider
    {
        private string? initialToken;

        public LiveShareRelayTokenProvider(
            ITunnelManagementClient managementClient,
            Tunnel tunnel,
            string hostId,
            string tokenScope,
            string? initialToken = null)
        {
            ManagementClient = Requires.NotNull(managementClient, nameof(managementClient));
            Tunnel = Requires.NotNull(tunnel, nameof(tunnel));
            HostId = Requires.NotNull(hostId, nameof(hostId));
            TokenScope = Requires.NotNull(tokenScope, nameof(tokenScope));
            this.initialToken = initialToken;
        }

        private ITunnelManagementClient ManagementClient { get; }

        private Tunnel Tunnel { get; }

        private string HostId { get; }

        private string TokenScope { get; set; }

        protected override async Task<SecurityToken> OnGetTokenAsync(
            string audience,
            TimeSpan validFor)
        {
            string? sasToken = null;
            if (this.initialToken != null)
            {
                sasToken = initialToken;
                this.initialToken = null;
            }
            else
            {
                // TODO: Refresh the tunnel access token first if necessary.

                string? tunnelAccessToken = null;
                Tunnel.AccessTokens?.TryGetValue(TokenScope, out tunnelAccessToken);

                var refreshedTunnel = await ManagementClient.GetTunnelAsync(
                    Tunnel,
                    new TunnelRequestOptions
                    {
                        TokenScopes = new[] { TokenScope },
                        AccessToken = tunnelAccessToken,
                    },
                    CancellationToken.None);

                var endpoint = refreshedTunnel?.Endpoints?.OfType<LiveShareRelayTunnelEndpoint>().
                    SingleOrDefault((ep) => ep.HostId == HostId);
                sasToken = TokenScope == TunnelAccessScopes.Host ?
                    endpoint?.RelayHostSasToken : endpoint?.RelayClientSasToken;

                if (string.IsNullOrEmpty(sasToken))
                {
                    throw new InvalidOperationException("Relay token was not returned by service.");
                }
            }

            var sasTokenProvider = CreateSharedAccessSignatureTokenProvider(sasToken);
            return await sasTokenProvider.GetTokenAsync(audience, validFor);
        }
    }
}
