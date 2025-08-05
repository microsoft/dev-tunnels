// <copyright file="RetryingTunnelConnectionEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Event args for tunnel connection retry event.
/// </summary>
public class RetryingTunnelConnectionEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="RetryingTunnelConnectionEventArgs"/> class.
    /// </summary>
    public RetryingTunnelConnectionEventArgs(Exception exception, int attemptNumber, TimeSpan delay)
    {
        Exception = Requires.NotNull(exception, nameof(exception));
        AttemptNumber = attemptNumber;
        Retry = true;
        Delay = delay;
    }

    /// <summary>
    /// Gets the exception that caused the retry.
    /// </summary>
    /// <remarks>
    /// For an au
    /// </remarks>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the attempt number for the retry.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the amount of time to wait before retrying. An event handler may change this value
    /// to adjust the delay.
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the retry will proceed. An event handler may
    /// set this to false to stop retrying.
    /// </summary>
    public bool Retry { get; set; }
}
