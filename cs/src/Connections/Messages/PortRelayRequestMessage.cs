// <copyright file="PortRelayRequestMessage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Text;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.IO;

namespace Microsoft.DevTunnels.Connections.Messages;

/// <summary>
/// Extends port-forward request messagse to include additional properties required
/// by the tunnel relay.
/// </summary>
public class PortRelayRequestMessage : PortForwardRequestMessage
{
    /// <summary>
    /// Access token with 'host' scope used to authorize the port-forward request.
    /// </summary>
    /// <remarks>
    /// A long-running host may need to handle the
    /// <see cref="ITunnelHost.RefreshingTunnelAccessToken" /> event to refresh the access token
    /// before forwarding additional ports.
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
