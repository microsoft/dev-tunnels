using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.DevTunnels.Contracts.Validation;

namespace Microsoft.DevTunnels.Contracts;

using static TunnelConstraints;

/// <summary>
/// Tunnel type used for tunnel service API version 2023-05-23-preview
/// </summary>
public class TunnelV1 : TunnelBase
{

    /// <summary>
    /// Initializes a new instance of the <see cref="TunnelV1"/> class.
    /// </summary>
    public TunnelV1()
    {
    }

    /// <summary>
    /// Gets or sets the ID of the tunnel, unique within the cluster.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [RegularExpression(TunnelV1IdPattern)]
    [StringLength(TunnelV1IdLength, MinimumLength = TunnelV1IdLength)]
    public override string? TunnelId { get; set; }

    /// <summary>
    /// Gets or sets the tags of the tunnel.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [MaxLength(MaxTags)]
    [ArrayStringLength(TagMaxLength, MinimumLength = TagMinLength)]
    [ArrayRegularExpression(TagPattern)]
    public string[]? Tags { get; set; }
}
