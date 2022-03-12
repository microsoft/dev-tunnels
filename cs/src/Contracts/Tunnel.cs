// <copyright file="Tunnel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for tunnel objects managed through the tunnel service REST API.
    /// </summary>
    public class Tunnel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tunnel"/> class.
        /// </summary>
        public Tunnel()
        {
        }

        /// <summary>
        /// Gets or sets the ID of the cluster the tunnel was created in.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ClusterId { get; set; }

        /// <summary>
        /// Gets or sets the generated ID of the tunnel, unique within the cluster.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TunnelId { get; set; }

        /// <summary>
        /// Gets or sets the optional short name (alias) of the tunnel.
        /// </summary>
        /// <remarks>
        /// The name must be globally unique within the parent domain, and must be a valid
        /// subdomain.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the tunnel.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the tags of the tunnel.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Tags { get; set; }

        /// <summary>
        /// Gets or sets the optional parent domain of the tunnel, if it is not using
        /// the default parent domain.
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Gets or sets a dictionary mapping from scopes to tunnel access tokens.
        /// </summary>
        /// <seealso cref="TunnelAccessScopes" />
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IDictionary<string, string>? AccessTokens { get; set; }

        /// <summary>
        /// Gets or sets access control settings for the tunnel.
        /// </summary>
        /// <remarks>
        /// See <see cref="TunnelAccessControl" /> documentation for details about the
        /// access control model.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TunnelAccessControl? AccessControl { get; set; }

        /// <summary>
        /// Gets or sets default options for the tunnel.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TunnelOptions? Options { get; set; }

        /// <summary>
        /// Gets or sets current connection status of the tunnel.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TunnelStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets an array of endpoints where hosts are currently accepting
        /// client connections to the tunnel.
        /// </summary>
        public TunnelEndpoint[]? Endpoints { get; set; }

        /// <summary>
        /// Gets or sets a list of ports in the tunnel.
        /// </summary>
        /// <remarks>
        /// This optional property enables getting info about all ports in a tunnel at the same time
        /// as getting tunnel info, or creating one or more ports at the same time as creating a
        /// tunnel. It is omitted when listing (multiple) tunnels, or when updating tunnel
        /// properties. (For the latter, use APIs to create/update/delete individual ports instead.)
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TunnelPort[]? Ports { get; set; }

        /// <summary>
        /// Gets or sets the time in UTC of tunnel creation.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? Created { get; set; }
    }
}
