// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.


import * as assert from 'assert';
import { suite, test, slow, timeout, pending, params } from '@testdeck/mocha';
import { MockUserManager } from './mocks/mockUserManager';
import { MockTunnelManagementClient } from './mocks/mockTunnelManagementClient';
import { UserInfo } from './userInfo';
import { ForwardedPortsCollection, LocalPortForwarder } from '@microsoft/dev-tunnels-ssh-tcp';
import { Tunnel, TunnelPort, TunnelConnectionMode } from '@microsoft/dev-tunnels-contracts';
import { TunnelManagementClient } from '@microsoft/dev-tunnels-management';
import { TunnelClient, TunnelConnectionBase, TunnelHost } from '@microsoft/dev-tunnels-connections';
import { CancellationToken, SshStream } from '@microsoft/dev-tunnels-ssh';
import { TunnelConnectionOptions } from 'src/connections/tunnelConnectionOptions';
import { Event } from 'vscode-jsonrpc';
import { PortForwardingEventArgs } from 'src/connections/portForwardingEventArgs';

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

class MockTunnelClient extends TunnelConnectionBase implements TunnelClient {
    forwardedPorts: ForwardedPortsCollection | undefined;
    public connectionModes: TunnelConnectionMode[] = [];
    public acceptLocalConnectionsForForwardedPorts = true;
    public localForwardingHostAddress = '127.0.0.1';
    connect(
        tunnel: Tunnel,
        options?: TunnelConnectionOptions,
        cancellation?: CancellationToken,
    ): Promise<any> {
        throw new Error('Method not implemented.');
    }
    forwardPort(tunnelPort: TunnelPort): Promise<LocalPortForwarder> {
        throw new Error('Method not implemented.');
    }

    public onConnected?: Function;
    public onForwarding?: Function;

    public get portForwarding() : Event<PortForwardingEventArgs> {
        throw new Error('Method not implemented.');
    }
    connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<SshStream> {
        throw new Error('Method not implemented.');
    }
    waitForForwardedPort(forwardedPort: number, cancellation?: CancellationToken): Promise<void> {
        throw new Error('Method not implemented.');
    }
    refreshPorts(): Promise<void> {
        throw new Error('Method not implemented.');
    }

}
