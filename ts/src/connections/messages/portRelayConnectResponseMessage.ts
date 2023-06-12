// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { ChannelOpenConfirmationMessage, SshDataReader, SshDataWriter } from "@microsoft/dev-tunnels-ssh";

/**
 * Extends port-forward channel open confirmation messages to include additional properties
 * required by the tunnel relay.
 */
export class PortRelayConnectResponseMessage extends ChannelOpenConfirmationMessage {
    /**
     * Gets or sets a value indicating whether end-to-end encryption is enabled for the
     * connection.
     * The tunnel client may request E2E encryption via `isE2EEncryptionRequested`. Then relay
     * or host may enable E2E encryption or not depending on capabilities and policies, and the
     * resulting enabled status is returned to the client via this property.
     */
    public isE2EEncryptionEnabled: boolean = false;

    protected onWrite(writer: SshDataWriter): void {
        super.onWrite(writer);

        writer.writeBoolean(this.isE2EEncryptionEnabled);
    }

    protected onRead(reader: SshDataReader): void {
        super.onRead(reader);

        this.isE2EEncryptionEnabled = reader.readBoolean();
    }
}
