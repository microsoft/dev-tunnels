// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelManagementClient, TunnelRequestOptions } from '@microsoft/dev-tunnels-management';
import {
    Tunnel,
    TunnelRelayTunnelEndpoint,
    TunnelPort,
    TunnelConnectionMode,
    TunnelEndpoint,
    ClusterDetails,
    NamedRateStatus,
} from '@microsoft/dev-tunnels-contracts';

export class MockTunnelManagementClient implements TunnelManagementClient {
    private idCounter: number = 0;
    public tunnels: Tunnel[] = [];
    public hostRelayUri?: string;
    public clientRelayUri?: string;

    listTunnels(
        clusterId?: string,
        domain?: string,
        options?: TunnelRequestOptions,
    ): Promise<Tunnel[]> {
        let tunnels = this.tunnels;

        if (options?.tags) {
            if (!options.requireAllTags) {
                tunnels = this.tunnels.filter(
                    (tunnel) => tunnel.tags && options.tags!.some((t) => tunnel.tags!.includes(t)),
                );
            } else {
                tunnels = this.tunnels.filter(
                    (tunnel) => tunnel.tags && options.tags!.every((t) => tunnel.tags!.includes(t)),
                );
            }
        }

        return Promise.resolve(tunnels);
    }

    async getTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel | null> {
        const clusterId = tunnel.clusterId;
        const tunnelId = tunnel.tunnelId;
        const name = tunnel.name;

        const t = this.tunnels.find(
            (t) =>
                (name && (t.name === name || t.tunnelId === name)) ||
                (t.clusterId === clusterId && t.tunnelId === tunnelId),
        );

        if (!t) {
            return null;
        }

        this.issueMockTokens(t, options);
        return t;
    }

    async createTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel> {
        if (await this.getTunnel(tunnel, options)) {
            throw new Error('Tunnel already exists.');
        }

        tunnel.tunnelId = 'tunnel' + ++this.idCounter;
        tunnel.clusterId = 'localhost';
        this.tunnels.push(tunnel);

        this.issueMockTokens(tunnel, options);
        return tunnel;
    }

    updateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel> {
        this.tunnels.forEach((t) => {
            if (t.clusterId == tunnel.clusterId && t.tunnelId == tunnel.tunnelId) {
                if (tunnel.name) {
                    t.name = tunnel.name;
                }

                if (tunnel.options) {
                    t.options = tunnel.options;
                }

                if (tunnel.accessControl) {
                    t.accessControl = tunnel.accessControl;
                }
            }
        });

        this.issueMockTokens(tunnel, options);

        return Promise.resolve(tunnel);
    }

    deleteTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<boolean> {
        for (let i = 0; i < this.tunnels.length; i++) {
            let t = this.tunnels[i];
            if (t.clusterId == tunnel.clusterId && t.tunnelId == tunnel.tunnelId) {
                //this.tunnels.RemoveAt(i);
                return new Promise<boolean>((resolve) => {
                    resolve(true);
                });
            }
        }

        return Promise.resolve(false);
    }

    updateTunnelEndpoint(
        tunnel: Tunnel,
        endpoint: TunnelEndpoint,
        options?: TunnelRequestOptions,
    ): Promise<TunnelEndpoint> {
        if (!tunnel.endpoints) {
            tunnel.endpoints = [];
        }

        for (let i = 0; i < tunnel.endpoints.length; i++) {
            if (
                tunnel.endpoints[i].hostId == endpoint.hostId &&
                tunnel.endpoints[i].connectionMode == endpoint.connectionMode
            ) {
                tunnel.endpoints[i] = endpoint;
                return new Promise<TunnelEndpoint>((resolve) => {
                    resolve(endpoint);
                });
            }
        }

        let newArray: TunnelEndpoint[] = Object.assign([], tunnel.endpoints);
        newArray.push(endpoint);
        tunnel.endpoints = newArray;

        let tunnelEndpoint: TunnelRelayTunnelEndpoint = endpoint;
        if (tunnelEndpoint) {
            tunnelEndpoint.hostRelayUri = this.hostRelayUri;
            tunnelEndpoint.clientRelayUri = this.clientRelayUri;
        }

        return Promise.resolve(endpoint);
    }

    deleteTunnelEndpoints(
        tunnel: Tunnel,
        hostId: string,
        connectionMode?: TunnelConnectionMode,
        options?: TunnelRequestOptions,
    ): Promise<boolean> {
        if (!hostId) {
            throw new Error('Host ID cannot be empty');
        }

        if (!tunnel.endpoints) {
            return new Promise<boolean>((resolve) => {
                resolve(false);
            });
        }

        let initialLength = tunnel.endpoints.length;
        tunnel.endpoints = tunnel.endpoints.filter(
            (ep) =>
                ep.hostId == hostId &&
                (connectionMode == null || ep.connectionMode == connectionMode),
        );
        return Promise.resolve(tunnel.endpoints!.length < initialLength);
    }

    listTunnelPorts(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<TunnelPort[]> {
        throw new Error('Method not implemented.');
    }

    getTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort | null> {
        throw new Error('Method not implemented.');
    }

    createTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort> {
        tunnelPort = {
            tunnelId: tunnel.tunnelId,
            clusterId: tunnel.clusterId,
            portNumber: tunnelPort.portNumber,
            protocol: tunnelPort.protocol,
            accessControl: tunnelPort.accessControl,
            options: tunnelPort.options,
        };
        tunnel.ports = tunnel.ports ? tunnel.ports.concat(tunnelPort) : undefined;
        return Promise.resolve(tunnelPort);
    }

    updateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort> {
        throw new Error('Method not implemented.');
    }

    deleteTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<boolean> {
        if (tunnel.ports) {
            const tunnelPort = tunnel.ports.find((p) => p.portNumber === portNumber);
            if (tunnelPort) {
                tunnel.ports = tunnel.ports.filter((p) => p !== tunnelPort);
                return Promise.resolve(true);
            }
        }

        return Promise.resolve(false);
    }

    listUserLimits(): Promise<NamedRateStatus[]> {
        throw new Error('Method not implemented.');
    }

    listClusters(): Promise<ClusterDetails[]> {
        throw new Error('Method not implemented.');
    }

    checkNameAvailablility(tunnelName: string): Promise<boolean> {        
        throw new Error('Method not implemented.');
    }

    private issueMockTokens(tunnel: Tunnel, options?: TunnelRequestOptions) {
        if (tunnel && options?.tokenScopes) {
            tunnel.accessTokens = {};
            options.tokenScopes.forEach((scope) => {
                tunnel.accessTokens![scope] = 'mock-token';
            });
        }
    }
}
