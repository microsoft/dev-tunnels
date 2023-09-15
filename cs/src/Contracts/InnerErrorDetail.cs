// <copyright file="InnerError.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// An object containing more specific information than the current object about the error.
/// </summary>
public class InnerErrorDetail
{
    /// <summary>
    /// A more specific error code than was provided by the containing error.
    /// One of a server-defined set of error codes in <see cref="ErrorCodes"/>.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// An object containing more specific information than the current object about the error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("innererror")]
    public InnerErrorDetail? InnerError { get; set; }
}
