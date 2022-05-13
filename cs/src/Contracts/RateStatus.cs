// <copyright file="RateStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Current value and limit information for a rate-limited operation related to a tunnel or port.
/// </summary>
public class RateStatus : ResourceStatus
{
    /// <summary>
    /// Gets or sets the length of each period, in seconds, over which the rate is measured.
    /// </summary>
    /// <remarks>
    /// For rates that are limited by month (or billing period), this value may represent
    /// an estimate, since the actual duration may vary by the calendar.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? PeriodSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds until the current measurement period ends and the
    /// current rate value resets.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? ResetSeconds { get; set; }
}
