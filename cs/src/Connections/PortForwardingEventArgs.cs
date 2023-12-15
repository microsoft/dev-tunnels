// <copyright file="PortForwardingEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Event raised when a port is about to be forwarded to the tunnel client.
/// </summary>
public class PortForwardingEventArgs : EventArgs
{
    /// <summary>
    /// Create a new instance of <see cref="PortForwardingEventArgs"/>.
    /// </summary>
    public PortForwardingEventArgs(int portNumber)
    {
        PortNumber = portNumber;
    }

    /// <summary>
    /// Gets the port number that is being forwarded.
    /// </summary>
    public int PortNumber { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the current forward request will be cancelled
    /// (ignored by this client).
    /// </summary>
    /// <remarks>
    /// Cancelling the event prevents the current port from being forwarded to the client. It
    /// prevents the tunnel client from listening on a local socket for the port, AND prevents
    /// use of <see cref="ITunnelClient.ConnectToForwardedPortAsync"/> to open a direct stream
    /// connection to the port.
    /// </remarks>
    public bool Cancel { get; set; }
}
