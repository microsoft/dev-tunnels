// <copyright file="PortRelayConnectResponseMessage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.IO;

namespace Microsoft.DevTunnels.Connections.Messages;

/// <summary>
/// Extends port-forward channel open confirmation messages to include additional properties
/// required by the tunnel relay.
/// </summary>
public class PortRelayConnectResponseMessage : ChannelOpenConfirmationMessage
{
    /// <summary>
    /// Gets or sets a value indicating whether end-to-end encryption is enabled for the
    /// connection.
    /// </summary>
    /// <remarks>
    /// The tunnel client may request E2E encryption via
    /// <see cref="PortRelayConnectRequestMessage.IsE2EEncryptionRequested"/>. Then relay or host
    /// may enable E2E encryption or not depending on capabilities and policies, and the resulting
    /// enabled status is returned to the client via this property.
    /// </remarks>
    public bool IsE2EEncryptionEnabled { get; set; }

    /// <inheritdoc/>
    protected override void OnWrite(ref SshDataWriter writer)
    {
        base.OnWrite(ref writer);

        writer.Write(IsE2EEncryptionEnabled);
    }

    /// <inheritdoc/>
    protected override void OnRead(ref SshDataReader reader)
    {
        base.OnRead(ref reader);

        IsE2EEncryptionEnabled = reader.ReadBoolean();
    }
}
