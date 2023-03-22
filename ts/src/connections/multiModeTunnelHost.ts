// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Tunnel, TunnelAccessScopes, TunnelPort } from '@microsoft/dev-tunnels-contracts';
import { TunnelHost } from '.';
import { v4 as uuidv4 } from 'uuid';
import { TunnelConnectionBase } from './tunnelConnectionBase';

/**
 * Aggregation of multiple tunnel hosts.
 */
export class MultiModeTunnelHost extends TunnelConnectionBase implements TunnelHost {
    public static hostId: string = uuidv4();
    public hosts: TunnelHost[];

    public constructor() {
        super(TunnelAccessScopes.Host);
        this.hosts = [];
    }

    public async start(tunnel: Tunnel): Promise<void> {
        const startTasks: Promise<void>[] = [];

        this.hosts.forEach((host) => {
            startTasks.push(host.start(tunnel));
        });

        await Promise.all(startTasks);
    }

    public async refreshPorts(): Promise<void> {
        const refreshTasks: Promise<void>[] = [];

        this.hosts.forEach((host) => {
            refreshTasks.push(host.refreshPorts());
        });

        await Promise.all(refreshTasks);
    }

    public async dispose(): Promise<void> {
        await Promise.all(this.hosts.map((host) => host.dispose()));
        await super.dispose();
    }
}
