//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//

import * as assert from 'assert';
import { suite, test, slow, timeout, pending, params } from '@testdeck/mocha';
import { MockUserManager } from './mocks/mockUserManager';
import { MockTunnelManagementClient } from './mocks/mockTunnelManagementClient';
import { UserInfo } from './userInfo';
import { ForwardedPortsCollection, LocalPortForwarder } from '@vs/vs-ssh-tcp';
import { Tunnel, TunnelPort, TunnelConnectionMode } from '@vs/tunnels-contracts';
import { TunnelManagementClient } from '@vs/tunnels-management';
import { TunnelClient, TunnelHost } from '@vs/tunnels-connections';
import { CancellationToken, SshStream } from '@vs/vs-ssh';

@suite
@slow(3000)
@timeout(10000)
export class MetricsTests {
    @slow(10000)
    @timeout(20000)
    public static async before() {}

    @test
    public async connectTunnel() {
        const userManager = new MockUserManager();
        userManager.currentUser = userManager.loginUser;
        let managementClient = new MockTunnelManagementClient();
        managementClient.tunnels.push({
            clusterId: 'localhost',
            tunnelId: 'test',
            name: 'name1',
            ports: [
                {
                    ClusterId: 'localhost',
                    TunnelId: 'test',
                    portNumber: 2000,
                } as TunnelPort,
            ],
        } as Tunnel);
    }
}

class MockConnectOptions {
    private readonly managementClient: TunnelManagementClient;
    private readonly clientFactory?: Function;

    constructor(managementClient: TunnelManagementClient, clientFactory?: Function) {
        this.managementClient = managementClient;
        this.clientFactory = clientFactory;
    }

    public createManagementClient(user: UserInfo): TunnelManagementClient {
        return this.managementClient;
    }

    public CreateHost(
        managementClient: TunnelManagementClient,
        connectionModes: TunnelConnectionMode[],
    ): TunnelHost {
        throw new Error('Not Supported Exception');
    }

    public CreateClient(
        managementClient: TunnelManagementClient,
        connectionModes: TunnelConnectionMode[],
    ): TunnelClient {
        if (this.clientFactory) {
            return this.clientFactory();
        } else {
            throw new Error('Not Supported Exception');
        }
    }
}

class MockTunnelClient implements TunnelClient {
    forwardedPorts: ForwardedPortsCollection | undefined;
    public connectionModes: TunnelConnectionMode[] = [];
    public acceptLocalConnectionsForForwardedPorts = true;
    connect(tunnel: Tunnel, hostId?: string): Promise<any> {
        throw new Error('Method not implemented.');
    }
    forwardPort(tunnelPort: TunnelPort): Promise<LocalPortForwarder> {
        throw new Error('Method not implemented.');
    }

    public onConnected?: Function;
    public onForwarding?: Function;

    connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<SshStream> {
        throw new Error('Method not implemented.');
    }
    waitForForwardedPort(forwardedPort: number, cancellation?: CancellationToken): Promise<void> {
        throw new Error('Method not implemented.');
    }

    dispose(): void {
        throw new Error('Method not implemented.');
    }
}
