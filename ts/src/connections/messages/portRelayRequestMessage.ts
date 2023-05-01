// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { SshDataReader, SshDataWriter } from "@microsoft/dev-tunnels-ssh";
import { PortForwardRequestMessage } from "@microsoft/dev-tunnels-ssh-tcp";

/**
 * Extends port-forward request messagse to include additional properties required
 * by the tunnel relay.
 */
export class PortRelayRequestMessage extends PortForwardRequestMessage {
    /**
     * Access token with 'host' scope used to authorize the port-forward request.
     * A long-running host may need to handle the `refreshingTunnelAccessToken` event to
     * refresh the access token before forwarding additional ports.
     */
    public accessToken?: string;

    protected onWrite(writer: SshDataWriter): void {
        super.onWrite(writer);

        if (!this.accessToken) {
            throw new Error("An access token is required.");
        }

        writer.writeString(this.accessToken, 'utf8');
    }

    protected onRead(reader: SshDataReader): void {
        super.onRead(reader);

        this.accessToken = reader.readString('utf8');
    }
}
