// <copyright file="TunnelEvent.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.DevTunnels.Contracts.Validation;

namespace Microsoft.DevTunnels.Contracts;

using static TunnelConstraints;

/// <summary>
/// Data contract for tunnel client events reported to the tunnel service.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public class TunnelEvent
{
    /// <summary>Default event severity.</summary>
    public const string Info = "info";

    /// <summary>Warning event severity.</summary>
    public const string Warning = "warning";

    /// <summary>Error event severity.</summary>
    public const string Error = "error";

    /// <summary>
    /// Initializes a new empty instance of the <see cref="TunnelEvent"/> class
    /// (for deserialization purposes).
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TunnelEvent()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TunnelEvent"/> class with a specified event
    /// name, and timestamp initialized to the current time.
    /// </summary>
    /// <param name="eventName">The required name of the event.</param>
    public TunnelEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            throw new ArgumentNullException(
                nameof(eventName),
                "Tunnel event name cannot be null or empty.");
        }

        Name = eventName;
        Timestamp = DateTime.UtcNow;
        Properties = new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets or sets the UTC timestamp of the event (using the client's clock).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets name of the event. This should be a short descriptive identifier.
    /// </summary>
    [RegularExpression(EventNamePattern)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the severity of the event, such as <see cref="Info"/>,
    /// <see cref="Warning"/>, or <see cref="Error"/>.
    /// </summary>
    /// <remarks>
    /// If not specified, the default severity is "info".
    /// </remarks>
    [RegularExpression(EventSeverityPattern)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Severity { get; set; }

    /// <summary>
    /// Gets or sets optional unstructured details about the event, such as a message or
    /// description. For warning or error events this may include a stack trace.
    /// </summary>
    [StringLength(EventDetailsMaxLength)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets semi-structured event properties.
    /// </summary>
    [DictionaryCount(MaxEventProperties)]
    [DictionaryRegularExpression(EventPropertyNamePattern)]
    [DictionaryStringLength(EventPropertyValueMaxLength)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string>? Properties { get; set; }

    /// <summary>
    /// Returns a string representation of the tunnel event.
    /// </summary>
    public override string ToString()
    {
        var s = $"{Timestamp:s} [{Severity ?? Info}] {Name}";

        if (!string.IsNullOrEmpty(Details))
        {
            s += $": {Details}";
        }

        if (Properties?.Count > 0)
        {
            s += $"; {string.Join(", ", Properties.Select(p => $"{p.Key}={p.Value}"))}";
        }

        return s;
    }
}
