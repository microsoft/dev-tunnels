// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import {
    ClusterDetails,
    NamedRateStatus,
    Tunnel,
    TunnelEndpoint,
    TunnelEvent,
    TunnelPort,
} from '@microsoft/dev-tunnels-contracts';
import { TunnelRequestOptions } from './tunnelRequestOptions';
import * as https from 'https';
import { CancellationToken } from 'vscode-jsonrpc';

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
     * The list can be filtered by setting `TunnelRequestOptions.labels`. Ports will not be
     * included in the returned tunnels unless `TunnelRequestOptions.includePorts` is set to true.
     *
     * @param clusterId A tunnel cluster ID, or null to list tunnels globally.
     * @param domain Tunnel domain, or null for the default domain.
     * @param options Request options.
     * @param cancellation Optional cancellation token for the request.
     */
    listTunnels(
        clusterId?: string,
        domain?: string,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
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
     * @param cancellation Optional cancellation token for the request.
     */
    getTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel | null>;

    /**
     * Creates a tunnel.
     * @param tunnel
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    createTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel>;

    /**
     * Updates properties of a tunnel.
     * @param tunnel
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    updateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel>;

    /**
     * Updates properties of a tunnel or creates it if it does not exist.
     * @param tunnel
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    createOrUpdateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel>;

    /**
     * Deletes a tunnel.
     * @param tunnel
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    deleteTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<boolean>;

    /**
     * Creates or updates an endpoint for the tunnel.
     * @param tunnel
     * @param endpoint
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    updateTunnelEndpoint(
        tunnel: Tunnel,
        endpoint: TunnelEndpoint,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelEndpoint>;

    /**
     * Deletes a tunnel endpoint.
     * @param tunnel
     * @param id
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    deleteTunnelEndpoints(
        tunnel: Tunnel,
        id: string,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<boolean>;

    /**
     * Lists ports on a tunnel.
     *
     * The list can be filtered by setting `TunnelRequestOptions.labels`.
     *
     * @param tunnel Tunnel object including at least either a tunnel name (globally unique,
     * if configured) or tunnel ID and cluster ID.
     * @param options Request options.
     * @param cancellation Optional cancellation token for the request.
     */
    listTunnelPorts(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<TunnelPort[]>;

    /**
     * Gets one port on a tunnel by port number.
     * @param tunnel
     * @param portNumber
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    getTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort | null>;

    /**
     * Creates a tunnel port.
     * @param tunnel
     * @param tunnelPort
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    createTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort>;

    /**
     * Updates properties of a tunnel port.
     * @param tunnel
     * @param tunnelPort
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    updateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort>;

    /**
     * Updates properties of a tunnel port or creates it if it does not exist.
     * @param tunnel
     * @param tunnelPort
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    createOrUpdateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort>;

    /**
     * Deletes a tunnel port.
     * @param tunnel
     * @param portNumber
     * @param options
     * @param cancellation Optional cancellation token for the request.
     */
    deleteTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<boolean>;

    /**
     * Lists limits and consumption status for the calling user.
     * @param cancellation Optional cancellation token for the request.
     */
    listUserLimits(cancellation?: CancellationToken): Promise<NamedRateStatus[]>;

    /**
     * Lists details of tunneling service clusters in all supported Azure regions.
     * @param cancellation Optional cancellation token for the request.
     */
    listClusters(cancellation?: CancellationToken): Promise<ClusterDetails[]>;

    /**
     * Checks if the tunnel name is available.
     * @param tunnelName
     * @param cancellation Optional cancellation token for the request.
     */
    checkNameAvailablility(tunnelName: string, cancellation?: CancellationToken): Promise<boolean>;

    /**
     * Reports a tunnel event to the tunnel service.
     * 
     * This method does not block; events are batched and uploaded by a background task.
     * The tunnel service and SDK automatically record some events related to tunnel operations
     * and connections. This method allows applications to report additional custom events.
     * @param tunnel Tunnel that the event is associated with.
     * @param tunnelEvent Event to report.
     * @param options Optional request options.
     */
    reportEvent(tunnel: Tunnel, tunnelEvent: TunnelEvent, options?: TunnelRequestOptions): void;

    /**
     * Disposes the client and any background tasks.
     */
    dispose(): Promise<void>;
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
