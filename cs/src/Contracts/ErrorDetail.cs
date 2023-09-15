// <copyright file="ErrorDetail.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// The top-level error object whose code matches the x-ms-error-code response header
/// </summary>
public class ErrorDetail
{
    /// <summary>
    /// One of a server-defined set of error codes defined in <see cref="ErrorCodes"/>.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// A human-readable representation of the error.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// The target of the error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    /// <summary>
    /// An array of details about specific errors that led to this reported error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetail[]? Details { get; set; }

    /// <summary>
    /// An object containing more specific information than the current object about the error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("innererror")]
    public InnerErrorDetail? InnerError {  get; set; }
}
