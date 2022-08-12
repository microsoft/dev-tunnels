// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    ClusterDetails,
    Tunnel,
    TunnelConnectionMode,
    TunnelEndpoint,
    TunnelPort,
} from '@microsoft/dev-tunnels-contracts';
import { TunnelRequestOptions } from './tunnelRequestOptions';
import * as https from 'https';

/**
 * Interface for a client that manages tunnels and tunnel ports
 * via the tunnel service management API.
 */
export interface TunnelManagementClient {
    /**
     * Override https agent for axios requests.
     */
    httpsAgent?: https.Agent;

    /**
     * Lists tunnels that are owned by the caller.
     *
     * The list can be filtered by setting `TunnelRequestOptions.tags`. Ports will not be
     * included in the returned tunnels unless `TunnelRequestOptions.includePorts` is set to true.
     *
     * @param clusterId A tunnel cluster ID, or null to list tunnels globally.
     * @param domain Tunnel domain, or null for the default domain.
     * @param options Request options.
     */
    listTunnels(
        clusterId?: string,
        domain?: string,
        options?: TunnelRequestOptions,
    ): Promise<Tunnel[]>;

    /**
     * Gets one tunnel by ID or name.
     *
     * Ports will not be included in the returned tunnel unless `TunnelRequestOptions.includePorts`
     * is set to true.
     *
     * @param tunnel Tunnel object including at least either a tunnel name (globally unique,
     * if configured) or tunnel ID and cluster ID.
     * @param options Request options.
     */
    getTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel | null>;

    /**
     * Creates a tunnel.
     * @param tunnel
     * @param options
     */
    createTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel>;

    /**
     * Updates properties of a tunnel.
     * @param tunnel
     * @param options
     */
    updateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel>;

    /**
     * Deletes a tunnel.
     * @param tunnel
     * @param options
     */
    deleteTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<boolean>;

    /**
     * Creates or updates an endpoint for the tunnel.
     * @param tunnel
     * @param endpoint
     * @param options
     */
    updateTunnelEndpoint(
        tunnel: Tunnel,
        endpoint: TunnelEndpoint,
        options?: TunnelRequestOptions,
    ): Promise<TunnelEndpoint>;

    /**
     * Deletes a tunnel endpoint.
     * @param tunnel
     * @param hostId
     * @param connectionMode
     * @param options
     */
    deleteTunnelEndpoints(
        tunnel: Tunnel,
        hostId: string,
        connectionMode?: TunnelConnectionMode,
        options?: TunnelRequestOptions,
    ): Promise<boolean>;

    /**
     * Lists ports on a tunnel.
     *
     * The list can be filtered by setting `TunnelRequestOptions.tags`.
     *
     * @param tunnel Tunnel object including at least either a tunnel name (globally unique,
     * if configured) or tunnel ID and cluster ID.
     * @param options Request options.
     */
    listTunnelPorts(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<TunnelPort[]>;

    /**
     * Gets one port on a tunnel by port number.
     * @param tunnel
     * @param portNumber
     * @param options
     */
    getTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort | null>;

    /**
     * Creates a tunnel port.
     * @param tunnel
     * @param tunnelPort
     * @param options
     */
    createTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort>;

    /**
     * Updates properties of a tunnel port.
     * @param tunnel
     * @param tunnelPort
     * @param options
     */
    updateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort>;

    /**
     * Deletes a tunnel port.
     * @param tunnel
     * @param portNumber
     * @param options
     */
    deleteTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<boolean>;

    /**
     * Lists details of tunneling service clusters in all supported Azure regions.
     */
    listClusters(): Promise<ClusterDetails[]>;

    /**
     * Checks if the tunnel name is available.
     * @param tunnelName 
     */
    checkNameAvailable(tunnelName: string): Promise<boolean>;
}

/**
 * Interface for the user agent product information for TunnelManagementClient
 */
export interface ProductHeaderValue {
    /**
     * Product name.
     */
    name: string;

    /**
     * Product version. If not supplied, 'unknown' version is used.
     */
    version?: string;
}

export abstract class TunnelAuthenticationSchemes {
    /** Authentication scheme for AAD (or Microsoft account) access tokens. */
    public static readonly aad = 'aad';

    /** Authentication scheme for GitHub access tokens. */
    public static readonly github = 'github';

    /** Authentication scheme for tunnel access tokens. */
    public static readonly tunnel = 'tunnel';
}
