// <copyright file="SshKeepAliveFailureEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Event raised when a keep-alive message respose is not received.
/// </summary>
public class SshKeepAliveEventArgs : EventArgs
{
    /// <summary>
    /// Create a new instance of <see cref="SshKeepAliveEventArgs"/>.
    /// </summary>
	public SshKeepAliveEventArgs(int count)
	{
		Count = count;
	}

	/// <summary>
	/// The number of keep-alive messages that have been sent without a response.
	/// </summary>
	public int Count { get; }
}
