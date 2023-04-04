// <copyright file="PortRelayConnectRequestMessage.cs" company="Microsoft">
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
public class PortRelayConnectRequestMessage : PortForwardChannelOpenMessage
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

    /// <summary>
    /// Gets or sets a value indicating whether end-to-end encryption is requested for the
    /// connection.
    /// </summary>
    /// <remarks>
    /// The tunnel relay or tunnel host may enable E2E encryption or not depending on capabilities
    /// and policies. The channel open response will indicate whether E2E encryption is actually
    /// enabled for the connection.
    /// </remarks>
    public bool IsE2EEncryptionRequested { get; set; }

    /// <inheritdoc/>
    protected override void OnWrite(ref SshDataWriter writer)
    {
        base.OnWrite(ref writer);

        writer.Write(AccessToken ?? string.Empty, Encoding.UTF8);
        writer.Write(IsE2EEncryptionRequested);
    }

    /// <inheritdoc/>
    protected override void OnRead(ref SshDataReader reader)
    {
        base.OnRead(ref reader);

        AccessToken = reader.ReadString(Encoding.UTF8);
        IsE2EEncryptionRequested = reader.ReadBoolean();
    }
}
