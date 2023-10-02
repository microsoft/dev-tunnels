// <copyright file="TunnelPort.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.DevTunnels.Contracts.Validation;

namespace Microsoft.DevTunnels.Contracts;

using static TunnelConstraints;

/// <summary>
/// Data contract for tunnel port objects managed through the tunnel service REST API.
/// </summary>
public class TunnelPort
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TunnelPort"/> class.
    /// </summary>
    public TunnelPort()
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
    /// Gets or sets the IP port number of the tunnel port.
    /// </summary>
    public ushort PortNumber { get; set; }

    /// <summary>
    /// Gets or sets the optional short name of the port.
    /// </summary>
    /// <remarks>
    /// The name must be unique among named ports of the same tunnel.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [RegularExpression(TunnelNamePattern)]
    [StringLength(TunnelNameMaxLength)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the port.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the labels of the port.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [MaxLength(MaxLabels)]
    [ArrayStringLength(LabelMaxLength, MinimumLength = LabelMinLength)]
    [ArrayRegularExpression(LabelPattern)]
    public string[]? Labels { get; set; }

    /// <summary>
    /// Gets or sets the protocol of the tunnel port.
    /// </summary>
    /// <remarks>
    /// Should be one of the string constants from <see cref="TunnelProtocol"/>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(TunnelProtocol.MaxLength)]
    public string? Protocol { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this port is a default port for the tunnel.
    /// </summary>
    /// <remarks>
    /// A client that connects to a tunnel (by ID or name) without specifying a port number will
    /// connect to the default port for the tunnel, if a default is configured. Or if the tunnel
    /// has only one port then the single port is the implicit default.
    /// <para/>
    /// Selection of a default port for a connection also depends on matching the connection to the
    /// port <see cref="Protocol" />, so it is possible to configure separate defaults for distinct
    /// protocols like <see cref="TunnelProtocol.Http" /> and <see cref="TunnelProtocol.Ssh" />.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets a dictionary mapping from scopes to tunnel access tokens.
    /// </summary>
    /// <remarks>
    /// Unlike the tokens in <see cref="Tunnel.AccessTokens"/>, these tokens are restricted
    /// to the individual port.
    /// </remarks>
    /// <seealso cref="TunnelAccessScopes" />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? AccessTokens { get; set; }

    /// <summary>
    /// Gets or sets access control settings for the tunnel port.
    /// </summary>
    /// <remarks>
    /// See <see cref="TunnelAccessControl" /> documentation for details about the
    /// access control model.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelAccessControl? AccessControl { get; set; }

    /// <summary>
    /// Gets or sets options for the tunnel port.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelOptions? Options { get; set; }

    /// <summary>
    /// Gets or sets current connection status of the tunnel port.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TunnelPortStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the username for the ssh service user is trying to forward.
    /// </summary>
    /// <remarks>
    /// Should be provided if the <see cref="TunnelProtocol"/> is Ssh.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(TunnelNameMaxLength)]
    public string? SshUser { get; set; }

    /// <summary>
    /// Gets or sets web forwarding URIs.
    /// If set, it's a list of absolute URIs where the port can be accessed with web forwarding.
    /// </summary>
    public string[]? PortForwardingUris { get; set; }

    /// <summary>
    /// Gets or sets inspection URI.
    /// If set, it's an absolute URIs where the port's traffic can be inspected.
    /// </summary>
    public string? InspectionUri { get; set; }
}
