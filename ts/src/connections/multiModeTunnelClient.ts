// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelConnectionMode, Tunnel, TunnelAccessScopes } from '@vs/tunnels-contracts';
import { CancellationToken, SshStream } from '@vs/vs-ssh';
import { ForwardedPortsCollection } from '@vs/vs-ssh-tcp';
import { TunnelClient } from '.';
import { TunnelConnectionBase } from './tunnelConnectionBase';

/**
 * Tunnel client implementation that selects one of multiple available connection modes.
 */
export class MultiModeTunnelClient extends TunnelConnectionBase implements TunnelClient {
    public forwardedPorts: ForwardedPortsCollection | undefined;
    public clients: TunnelClient[] = [];

    public connectionModes: TunnelConnectionMode[] = this.clients
        ? [...new Set(...this.clients.map((c) => c.connectionModes))]
        : [];

    constructor() {
        super(TunnelAccessScopes.Connect);
    }

    /**
     * A value indicating whether local connections for forwarded ports are accepted.
     * Local connections are not accepted if the host process is not NodeJS (e.g. browser).
     */
    public get acceptLocalConnectionsForForwardedPorts(): boolean {
        return !!this.clients.find((c) => c.acceptLocalConnectionsForForwardedPorts);
    }

    public set acceptLocalConnectionsForForwardedPorts(value: boolean) {
        this.clients.forEach((c) => (c.acceptLocalConnectionsForForwardedPorts = value));
    }

    public connect(tunnel: Tunnel, hostId?: string): Promise<void> {
        if (!tunnel) {
            throw new Error('Tunnel cannot be null');
        }

        return new Promise<void>((resolve) => {});
    }

    public connectToForwardedPort(
        fowardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<SshStream> {
        throw new Error('Method not implemented.');
    }
    public waitForForwardedPort(
        forwardedPort: number,
        cancellation?: CancellationToken,
    ): Promise<void> {
        throw new Error('Method not implemented.');
    }

    public async dispose(): Promise<void> {
        await super.dispose();
        await Promise.all(this.clients.map((client) => client.dispose()));
    }
}
