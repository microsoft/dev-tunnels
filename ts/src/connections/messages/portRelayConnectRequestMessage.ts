// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { SshDataReader, SshDataWriter } from "@microsoft/dev-tunnels-ssh";
import { PortForwardChannelOpenMessage } from "@microsoft/dev-tunnels-ssh-tcp";

/**
 * Extends port-forward channel open messages to include additional properties required
 * by the tunnel relay.
 */
export class PortRelayConnectRequestMessage extends PortForwardChannelOpenMessage {
    /**
     * Access token with 'connect' scope used to authorize the port connection request.
     * A long-running client may need handle the `RefreshingTunnelAccessToken` event to refresh
     * the access token before opening additional connections (channels) to forwarded ports.
     */
    public accessToken?: string;

    /**
     * Gets or sets a value indicating whether end-to-end encryption is requested for the
     * connection.
     * The tunnel relay or tunnel host may enable E2E encryption or not depending on capabilities
     * and policies. The channel open response will indicate whether E2E encryption is actually
     * enabled for the connection.
     */
    public isE2EEncryptionRequested: boolean = false;

    protected onWrite(writer: SshDataWriter): void {
        super.onWrite(writer);

        writer.writeString(this.accessToken ?? '', 'utf8');
        writer.writeBoolean(this.isE2EEncryptionRequested);
    }

    protected onRead(reader: SshDataReader): void {
        super.onRead(reader);

        this.accessToken = reader.readString('utf8');
        this.isE2EEncryptionRequested = reader.readBoolean();
    }
}
