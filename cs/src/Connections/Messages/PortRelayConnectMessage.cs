// <copyright file="PortRelayConnectMessage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.IO;

namespace Microsoft.DevTunnels.Connections.Messages;

/// <summary>
/// Extends port-forward channel open messages to include additional properties required
/// by the tunnel relay.
/// </summary>
public class PortRelayConnectMessage : PortForwardChannelOpenMessage
{
    /// <summary>
    /// Access token with 'connect' scope used to authorize the port connection request.
    /// </summary>
    /// <remarks>
    /// A long-running client may need handle the
    /// <see cref="ITunnelHost.RefreshingTunnelAccessToken" /> event to refresh the access token
    /// before opening additional connections (channels) to forwarded ports.
    /// </remarks>
    public string? AccessToken { get; set; }

    /// <inheritdoc/>
    protected override void OnWrite(ref SshDataWriter writer)
    {
        base.OnWrite(ref writer);

        writer.Write(
            AccessToken ?? throw new InvalidOperationException("An access token is required."),
            Encoding.UTF8);
    }

    /// <inheritdoc/>
    protected override void OnRead(ref SshDataReader reader)
    {
        base.OnRead(ref reader);

        AccessToken = reader.ReadString(Encoding.UTF8);
    }
}
