// <copyright file="ResourceStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Current value and limit for a limited resource related to a tunnel or tunnel port.
/// </summary>
public class ResourceStatus
{
    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public ulong Current { get; set; }

    /// <summary>
    /// Gets or sets the limit enforced by the service, or null if there is no limit.
    /// </summary>
    /// <remarks>
    /// Any requests that would cause the limit to be exceeded may be denied by the service.
    /// For HTTP requests, the response is generally a 403 Forbidden status, with details about
    /// the limit in the response body.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? Limit { get; set; }
}
