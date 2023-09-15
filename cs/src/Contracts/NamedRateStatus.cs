// <copyright file="NamedRateStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// A named <see cref="RateStatus"/>.
/// </summary>
public class NamedRateStatus : RateStatus
{
    /// <summary>
    /// The name of the rate status.
    /// </summary>
    public string? Name { get; set; }
}
