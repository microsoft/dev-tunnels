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

    public async addPort(portToAdd: TunnelPort): Promise<TunnelPort> {
        let addTasks: Promise<TunnelPort>[] = [];
        this.hosts.forEach((host) => {
            addTasks.push(host.addPort(portToAdd));
        });
        await Promise.all(addTasks);

        return portToAdd;
    }

    public async removePort(portNumberToRemove: number): Promise<boolean> {
        let result = true;
        let removeTasks: Promise<boolean>[] = [];
        this.hosts.forEach((host) => {
            removeTasks.push(host.removePort(portNumberToRemove));
        });

        let results = await Promise.all(removeTasks);
        results.forEach((res) => {
            result = result && res;
        });

        return result;
    }

    public async updatePort(updatedPort: TunnelPort): Promise<TunnelPort> {
        let updateTasks: Promise<TunnelPort>[] = [];
        this.hosts.forEach((host) => {
            updateTasks.push(host.updatePort(updatedPort));
        });

        await Promise.all(updateTasks);
        return updatedPort;
    }

    public dispose(): void {
        this.hosts.forEach((host) => {
            host.dispose();
        });
    }
}
