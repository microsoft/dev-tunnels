// <copyright file="Tunnel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.DevTunnels.Contracts.Validation;

namespace Microsoft.DevTunnels.Contracts;

using static TunnelConstraints;

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
    [RegularExpression(ClusterIdPattern)]
    [StringLength(ClusterIdMaxLength, MinimumLength = ClusterIdMinLength)]
    public string? ClusterId { get; set; }

    /// <summary>
    /// Gets or sets the generated ID of the tunnel, unique within the cluster.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [RegularExpression(NewTunnelIdPattern)]
    [StringLength(NewTunnelIdMaxLength, MinimumLength = NewTunnelIdMinLength)]
    public string? TunnelId { get; set; }

    /// <summary>
    /// Gets or sets the optional short name (alias) of the tunnel.
    /// </summary>
    /// <remarks>
    /// The name must be globally unique within the parent domain, and must be a valid
    /// subdomain.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [RegularExpression(TunnelNamePattern)]
    [StringLength(TunnelNameMaxLength)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the tunnel.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the labels of the tunnel.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [MaxLength(MaxLabels)]
    [ArrayStringLength(LabelMaxLength, MinimumLength = LabelMinLength)]
    [ArrayRegularExpression(LabelPattern)]
    public string[]? Labels { get; set; }

    /// <summary>
    /// Gets or sets the optional parent domain of the tunnel, if it is not using
    /// the default parent domain.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [RegularExpression(TunnelDomainPattern)]
    [StringLength(TunnelDomainMaxLength)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [MaxLength(TunnelMaxPorts)]
    public TunnelPort[]? Ports { get; set; }

    /// <summary>
    /// Gets or sets the time in UTC of tunnel creation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Created { get; set; }

    /// <summary>
    /// Gets or the time the tunnel will be deleted if it is not used or updated.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Gets or the custom amount of time the tunnel will be valid if it is not used or updated in seconds.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? CustomExpiration { get; set; }
}
