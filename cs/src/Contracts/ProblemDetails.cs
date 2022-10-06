// <copyright file="ProblemDetails.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Structure of error details returned by the tunnel service, including validation errors.
    /// </summary>
    /// <remarks>
    /// This object may be returned with a response status code of 400 (or other 4xx code).
    /// It is compatible with RFC 7807 Problem Details (https://tools.ietf.org/html/rfc7807) and
    /// https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails
    /// but doesn't require adding a dependency on that package.
    /// </remarks>
    public class ProblemDetails
    {
        /// <summary>
        /// Gets or sets the error title.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the error detail.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; set; }

        /// <summary>
        /// Gets or sets additional details about individual request properties.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IDictionary<string, string[]>? Errors { get; set; }
    }
}
