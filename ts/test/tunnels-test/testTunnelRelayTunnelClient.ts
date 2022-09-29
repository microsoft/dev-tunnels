// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelRelayTunnelClient } from "@microsoft/tunnels-connections";

/**
 * Test TunnelRelayTunnelClient that exposes protected members for testing.
 */
export class TestTunnelRelayTunnelClient extends TunnelRelayTunnelClient {
    constructor() {
        super();
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