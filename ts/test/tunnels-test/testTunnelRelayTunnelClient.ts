// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelRelayTunnelClient } from "@microsoft/dev-tunnels-connections";
import { TunnelManagementClient } from "@microsoft/dev-tunnels-management";

/**
 * Test TunnelRelayTunnelClient that exposes protected members for testing.
 */
export class TestTunnelRelayTunnelClient extends TunnelRelayTunnelClient {
    constructor(managementClient?: TunnelManagementClient) {
        super(undefined, managementClient);
    }

    public get isSshSessionActiveProperty(): boolean {
        return this.isSshSessionActive;
    }

    public get sshSessionClosedEvent() {
        return this.sshSessionClosed;
    }

    public hasForwardedChannels(port: number): boolean {
        return super.hasForwardedChannels(port);
    }
}