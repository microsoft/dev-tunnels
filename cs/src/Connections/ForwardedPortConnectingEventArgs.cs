// <copyright file="RefreshingTunnelAccessTokenEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using Microsoft.DevTunnels.Ssh;
using System;

namespace Microsoft.DevTunnels.Connections;

/// <summary>
/// Event args for the forwarded port connecting event.
/// </summary>
public class ForwardedPortConnectingEventArgs : EventArgs {
    /// <summary>
    /// Creates a new instance of <see cref="ForwardedPortConnectingEventArgs"/> class.
    /// </summary>
    public ForwardedPortConnectingEventArgs(uint port, SshStream stream) {
        Port = port;
        Stream = stream;
    }

    /// <summary>
    ///  Forwarded port
    /// </summary>
    public uint Port { get; }

    /// <summary>
    /// Connection stream
    /// </summary>
    public SshStream Stream { get; }
}
