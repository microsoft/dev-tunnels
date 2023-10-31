// <copyright file="ConnectionStatusChangedEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Connection status change event args.
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Create a new instance of <see cref="ConnectionStatusChangedEventArgs"/>.
    /// </summary>
    public ConnectionStatusChangedEventArgs(ConnectionStatus previousStatus, ConnectionStatus status, Exception? disconnectException)
    {
        PreviousStatus = previousStatus;
        Status = status;
        DisconnectException = disconnectException;
    }

    /// <summary>
    /// Get the previous connection status.
    /// </summary>
    public ConnectionStatus PreviousStatus { get; }

    /// <summary>
    /// Get the current connection status.
    /// </summary>
    public ConnectionStatus Status { get; }

    /// <summary>
    /// Get the exception that caused disconnect if <see cref="Status"/> is <see cref="ConnectionStatus.Disconnected"/> and the tunnel client is not disposed yet.
    /// <c>null</c> if <see cref="Status"/> is not <see cref="ConnectionStatus.Disconnected"/> or the tunnel client is disposed.
    /// </summary>
    /// <seealso cref="TunnelConnection.DisconnectException"/>.
    public Exception? DisconnectException { get; }
}
