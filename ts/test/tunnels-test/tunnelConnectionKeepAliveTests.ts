// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { MockTunnelManagementClient } from './mocks/mockTunnelManagementClient';
import {
    Tunnel,
    TunnelConnectionMode,
    TunnelRelayTunnelEndpoint,
} from '@microsoft/dev-tunnels-contracts';
import {
    TunnelRelayTunnelClient,
    TunnelRelayTunnelHost,
    TunnelConnectionOptions,
    SshKeepAliveEventArgs,
} from '@microsoft/dev-tunnels-connections';
import {
    KeyPair,
    SshAlgorithms,
} from '@microsoft/dev-tunnels-ssh';

@suite
export class TunnelConnectionKeepAliveTests {
    private hostKeys!: KeyPair;
    private managementClient!: MockTunnelManagementClient;
    private tunnel!: Tunnel;

    public async before() {
        this.hostKeys = await SshAlgorithms.publicKey.ecdsaSha2Nistp384!.generateKeyPair();
        this.managementClient = new MockTunnelManagementClient();
        this.tunnel = {
            tunnelId: 'tunnel1',
            name: 'Test Tunnel',
            domain: 'localhost',
            accessTokens: {},
            endpoints: [
                {
                    id: 'endpoint1',
                    connectionMode: TunnelConnectionMode.TunnelRelay,
                    hostRelayUri: 'wss://localhost:8080/tunnel',
                    clientRelayUri: 'wss://localhost:8080/tunnel',
                } as TunnelRelayTunnelEndpoint,
            ],
        };
    }

    @test
    public async testClientKeepAlive() {
        const client = new TunnelRelayTunnelClient(this.managementClient);

        let failedEvent: SshKeepAliveEventArgs | undefined;
        let succeededEvent: SshKeepAliveEventArgs | undefined;

        const failedSubscription = client.keepAliveFailed((e: SshKeepAliveEventArgs) => {
            failedEvent = e;
        });

        const succeededSubscription = client.keepAliveSucceeded((e: SshKeepAliveEventArgs) => {
            succeededEvent = e;
        });

        (client as any).onKeepAliveFailed(1);
        (client as any).onKeepAliveSucceeded(1);

        assert.strictEqual(failedEvent?.count, 1, 'Failed event should fire');
        assert.strictEqual(succeededEvent?.count, 1, 'Succeeded event should fire');

        failedSubscription.dispose();
        succeededSubscription.dispose();
        await client.dispose();
    }

    @test
    public async testHostKeepAlive() {
        const host = new TunnelRelayTunnelHost(this.managementClient);

        let failedEvent: SshKeepAliveEventArgs | undefined;
        let succeededEvent: SshKeepAliveEventArgs | undefined;

        const failedSubscription = host.keepAliveFailed((e: SshKeepAliveEventArgs) => {
            failedEvent = e;
        });

        const succeededSubscription = host.keepAliveSucceeded((e: SshKeepAliveEventArgs) => {
            succeededEvent = e;
        });

        (host as any).onKeepAliveFailed(2);
        (host as any).onKeepAliveSucceeded(2);

        assert.strictEqual(failedEvent?.count, 2, 'Failed event should fire');
        assert.strictEqual(succeededEvent?.count, 2, 'Succeeded event should fire');


        failedSubscription.dispose();
        succeededSubscription.dispose();
        await host.dispose();
    }
}
