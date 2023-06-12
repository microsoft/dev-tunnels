// <copyright file="RateStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

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
    /// Gets or sets the unix time in seconds when this status will be reset.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ResetTime { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var count = base.ToString();
        if (PeriodSeconds == null)
        {
            return count;
        }

        if (PeriodSeconds.Value == 1)
        {
            return count + "/s";
        }
        else if (PeriodSeconds.Value == 60)
        {
            return count + "/m";
        }
        else if (PeriodSeconds.Value == 3600)
        {
            return count + "/h";
        }
        else if (PeriodSeconds.Value == (24*3600))
        {
            return count + "/d";
        }
        else if ((PeriodSeconds.Value % (24*3600)) == 0)
        {
            return $"{count}/{(PeriodSeconds.Value / (24*3600))}d";
        }
        else if (PeriodSeconds.Value % 3600 == 0)
        {
            return $"{count}/{PeriodSeconds.Value / 3600}h";
        }
        else if (PeriodSeconds.Value % 60 == 0)
        {
            return $"{count}/{PeriodSeconds.Value / 60}m";
        }
        else
        {
            return $"{count}/{PeriodSeconds.Value}s";
        }
    }
}
