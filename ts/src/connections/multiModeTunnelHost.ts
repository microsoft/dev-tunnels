// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelPort } from '@vs/tunnels-contracts';
import { TunnelHost } from '.';
import { v4 as uuidv4 } from 'uuid';

/**
 * Aggregation of multiple tunnel hosts.
 */
export class MultiModeTunnelHost implements TunnelHost {
    public static hostId: string = uuidv4();
    public hosts: TunnelHost[];

    constructor() {
        this.hosts = [];
    }

    public async start(tunnel: Tunnel): Promise<void> {
        let startTasks: Promise<void>[] = [];

        this.hosts.forEach((host) => {
            startTasks.push(host.start(tunnel));
        });

        await Promise.all(startTasks);
    }

    public async refreshPorts(): Promise<void> {
        let refreshTasks: Promise<void>[] = [];

        this.hosts.forEach((host) => {
            refreshTasks.push(host.refreshPorts());
        });

        await Promise.all(refreshTasks);
    }

    public dispose(): void {
        this.hosts.forEach((host) => {
            host.dispose();
        });
    }
}
