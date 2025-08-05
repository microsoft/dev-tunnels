// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationToken, Disposable, Emitter, Event } from 'vscode-jsonrpc';
import { PromiseCompletionSource } from '@microsoft/dev-tunnels-ssh';
import {
    Tunnel,
    TunnelAccessControl,
    TunnelAccessScopes,
    TunnelConstraints,
    TunnelEndpoint,
    TunnelPort,
    ProblemDetails,
    TunnelServiceProperties,
    ClusterDetails,
    NamedRateStatus,
    TunnelListByRegionResponse,
    TunnelPortListResponse,
    TunnelProgress,
    TunnelReportProgressEventArgs,
    TunnelEvent,
} from '@microsoft/dev-tunnels-contracts';
import {
    ProductHeaderValue,
    TunnelAuthenticationSchemes,
    TunnelManagementClient,
} from './tunnelManagementClient';
import { TunnelRequestOptions } from './tunnelRequestOptions';
import { TunnelAccessTokenProperties } from './tunnelAccessTokenProperties';
import { tunnelSdkUserAgent } from './version';
import axios, { AxiosAdapter, AxiosError, AxiosRequestConfig, AxiosResponse, Method } from 'axios';
import * as https from 'https';
import { TunnelPlanTokenProperties } from './tunnelPlanTokenProperties';
import { IdGeneration } from './idGeneration';

type NullableIfNotBoolean<T> = T extends boolean ? T : T | null;

const tunnelsApiPath = '/tunnels';
const limitsApiPath = '/userlimits';
const endpointsApiSubPath = '/endpoints';
const portsApiSubPath = '/ports';
const eventsApiSubPath = '/events';
const clustersApiPath = '/clusters';
const tunnelAuthentication = 'Authorization';
const checkAvailablePath = ':checkNameAvailability';
const createNameRetries = 3;
export enum ManagementApiVersions {
    Version20230927preview = '2023-09-27-preview',
}

function comparePorts(a: TunnelPort, b: TunnelPort) {
    return (a.portNumber ?? Number.MAX_SAFE_INTEGER) - (b.portNumber ?? Number.MAX_SAFE_INTEGER);
}

function parseDate(value?: string | Date) {
    return typeof value === 'string' ? new Date(Date.parse(value)) : value;
}

/**
 * Fixes Tunnel properties of type Date that were deserialized as strings.
 */
function parseTunnelDates(tunnel: Tunnel | null) {
    if (!tunnel) return;
    tunnel.created = parseDate(tunnel.created);
    if (tunnel.status) {
        tunnel.status.lastHostConnectionTime = parseDate(tunnel.status.lastHostConnectionTime);
        tunnel.status.lastClientConnectionTime = parseDate(tunnel.status.lastClientConnectionTime);
    }
}

/**
 * Fixes TunnelPort properties of type Date that were deserialized as strings.
 */
function parseTunnelPortDates(port: TunnelPort | null) {
    if (!port) return;
    if (port.status) {
        port.status.lastClientConnectionTime = parseDate(port.status.lastClientConnectionTime);
    }
}

/**
 * Copy access tokens from the request object to the result object, except for any
 * tokens that were refreshed by the request.
 */
function preserveAccessTokens<T extends Tunnel | TunnelPort>(
    requestObject: T,
    resultObject: T | null,
) {
    // This intentionally does not check whether any existing tokens are expired. So
    // expired tokens may be preserved also, if not refreshed. This allows for better
    // diagnostics in that case.
    if (requestObject.accessTokens && resultObject) {
        resultObject.accessTokens ??= {};
        for (const scopeAndToken of Object.entries(requestObject.accessTokens)) {
            if (!resultObject.accessTokens[scopeAndToken[0]]) {
                resultObject.accessTokens[scopeAndToken[0]] = scopeAndToken[1];
            }
        }
    }
}


const manageAccessTokenScope = [TunnelAccessScopes.Manage];
const hostAccessTokenScope = [TunnelAccessScopes.Host];
const managePortsAccessTokenScopes = [
    TunnelAccessScopes.Manage,
    TunnelAccessScopes.ManagePorts,
    TunnelAccessScopes.Host,
];
const readAccessTokenScopes = [
    TunnelAccessScopes.Manage,
    TunnelAccessScopes.ManagePorts,
    TunnelAccessScopes.Host,
    TunnelAccessScopes.Connect,
];
const apiVersions = ["2023-09-27-preview"];
const defaultRequestTimeoutMS = 20000;

interface EventInfo {
    tunnel: Tunnel;
    event: TunnelEvent;
    requestOptions?: TunnelRequestOptions;
}

export class TunnelManagementHttpClient implements TunnelManagementClient {
    public additionalRequestHeaders?: { [header: string]: string };
    public apiVersion: string;

    private readonly baseAddress: string;
    private readonly userTokenCallback: () => Promise<string | null>;
    private readonly userAgents: string;

    private readonly reportProgressEmitter = new Emitter<TunnelReportProgressEventArgs>();

    /**
     * Event that is raised to report tunnel management progress.
     *
     * See `Progress` for a description of the different progress events that can be reported.
     */
    public readonly onReportProgress: Event<TunnelReportProgressEventArgs> = this.reportProgressEmitter.event;

    public trace: (msg: string) => void = (msg) => {};

    /**
     * Gets or sets a value indicating whether events reporting is enabled.
     * 
     * When not enabled, any events reported via {@link reportEvent}
     * (either by the tunnel SDK or the application) will be ignored.
     */
    public enableEventsReporting: boolean = false;

    private readonly eventsQueue: EventInfo[] = [];
    private eventsPromise: Promise<void> | null = null;
    private isDisposed: boolean = false;
    private eventsAvailableCompletion = new PromiseCompletionSource<void>();

    /**
     * Initializes a new instance of the `TunnelManagementHttpClient` class
     * with a client authentication callback, service URI, and HTTP handler.
     *
     * @param userAgent { name, version } object or a comment string to use as the User-Agent header.
     * @param apiVersion ApiVersion to be used for requests, value should be one of ManagementApiVersions enum.
     * @param userTokenCallback Optional async callback for retrieving a client authentication
     * header value with access token, for AAD or GitHub user authentication. This may be omitted
     * for anonymous tunnel clients, or if tunnel access tokens will be specified via
     * `TunnelRequestOptions.accessToken`.
     * @param tunnelServiceUri Optional tunnel service URI (not including any path). Defaults to
     * the global tunnel service URI.
     * @param httpsAgent Optional agent that will be invoked for HTTPS requests to the tunnel
     * service.
     * @param adapter Optional axios adapter to use for HTTP requests.
     */
    public constructor(
        userAgents: (ProductHeaderValue | string)[] | ProductHeaderValue | string,
        apiVersion: ManagementApiVersions,
        userTokenCallback?: () => Promise<string | null>,
        tunnelServiceUri?: string,
        public readonly httpsAgent?: https.Agent,
        private readonly adapter?: AxiosAdapter
    ) {
        if (apiVersions.indexOf(apiVersion) === -1) {
            throw new TypeError(`Invalid API version: ${apiVersion}, must be one of ${apiVersions}`);
        }
        this.apiVersion = apiVersion;

        if (!userAgents) {
            throw new TypeError('User agent must be provided.');
        }

        if (Array.isArray(userAgents)) {
            if (userAgents.length === 0) {
                throw new TypeError('User agents cannot be empty.');
            }
            let combinedUserAgents = '';

            userAgents.forEach((userAgent) => {
                if (typeof userAgent !== 'string') {
                    if (!userAgent.name) {
                        throw new TypeError('Invalid user agent. The name must be provided.');
                    }

                    if (typeof userAgent.name !== 'string') {
                        throw new TypeError('Invalid user agent. The name must be a string.');
                    }

                    if (userAgent.version && typeof userAgent.version !== 'string') {
                        throw new TypeError('Invalid user agent. The version must be a string.');
                    }
                    combinedUserAgents = `${combinedUserAgents}${
                        userAgent.name
                    }/${userAgent.version ?? 'unknown'} `;
                } else {
                    combinedUserAgents = `${combinedUserAgents}${userAgent} `;
                }
            });
            this.userAgents = combinedUserAgents.trim();
        } else if (typeof userAgents !== 'string') {
            if (!userAgents.name) {
                throw new TypeError('Invalid user agent. The name must be provided.');
            }

            if (typeof userAgents.name !== 'string') {
                throw new TypeError('Invalid user agent. The name must be a string.');
            }

            if (userAgents.version && typeof userAgents.version !== 'string') {
                throw new TypeError('Invalid user agent. The version must be a string.');
            }
            this.userAgents = `${userAgents.name}/${userAgents.version ?? 'unknown'}`;
        } else {
            this.userAgents = userAgents;
        }

        this.userTokenCallback = userTokenCallback ?? (() => Promise.resolve(null));

        if (!tunnelServiceUri) {
            tunnelServiceUri = TunnelServiceProperties.production.serviceUri;
        }

        const parsedUri = new URL(tunnelServiceUri);
        if (!parsedUri || parsedUri.pathname !== '/') {
            throw new TypeError(`Invalid tunnel service URI: ${tunnelServiceUri}`);
        }

        this.baseAddress = tunnelServiceUri;
    }

    public async listTunnels(
        clusterId?: string,
        domain?: string,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<Tunnel[]> {
        const queryParams = [clusterId ? null : 'global=true', domain ? `domain=${domain}` : null];
        const query = queryParams.filter((p) => !!p).join('&');
        const results = (await this.sendRequest<TunnelListByRegionResponse>(
            'GET',
            clusterId,
            tunnelsApiPath,
            query,
            options,
            undefined,
            undefined,
            cancellation
        ))!;
        let tunnels = new Array<Tunnel>();
        if (results.value) {
            for (const region of results.value) {
                if (region.value) {
                    tunnels = tunnels.concat(region.value);
                }
            }
        }
        tunnels.forEach(parseTunnelDates);
        return tunnels;
    }

    public async getTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel | null> {
        const result = await this.sendTunnelRequest<Tunnel | null>(
            'GET',
            tunnel,
            readAccessTokenScopes,
            undefined,
            undefined,
            options,
            undefined,
            undefined,
            cancellation
        );
        preserveAccessTokens(tunnel, result);
        parseTunnelDates(result);
        return result;
    }

    public async createTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel> {
        const tunnelId = tunnel.tunnelId;
        const idGenerated = tunnelId === undefined || tunnelId === null || tunnelId === '';
        options = options || {};
        options.additionalHeaders = options.additionalHeaders || {};
        options.additionalHeaders['If-Not-Match'] = "*";

        if (idGenerated) {
            tunnel.tunnelId = IdGeneration.generateTunnelId();
        }
        for (let i = 0;i<=createNameRetries; i++){
            try {
                const result = (await this.sendTunnelRequest<Tunnel>(
                    'PUT',
                    tunnel,
                    manageAccessTokenScope,
                    undefined,
                    undefined,
                    options,
                    this.convertTunnelForRequest(tunnel),
                    undefined,
                    cancellation,
                    true,
                ))!;
                preserveAccessTokens(tunnel, result);
                parseTunnelDates(result);
                return result;
            } catch (error) {
                if (idGenerated) {
                    // The tunnel ID was generated and there was a conflict.
                    // Try again with a new ID.
                    tunnel.tunnelId = IdGeneration.generateTunnelId();
                } else {
                    throw error;
                }
            }
        }

        const result2 = (await this.sendTunnelRequest<Tunnel>(
            'PUT',
            tunnel,
            manageAccessTokenScope,
            undefined,
            undefined,
            options,
            this.convertTunnelForRequest(tunnel),
            undefined,
            cancellation,
            true,
        ))!;
        preserveAccessTokens(tunnel, result2);
        parseTunnelDates(result2);
        return result2;
    }

    public async createOrUpdateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel> {
        const tunnelId = tunnel.tunnelId;
        const idGenerated = tunnelId === undefined || tunnelId === null || tunnelId === '';
        if (idGenerated) {
            tunnel.tunnelId = IdGeneration.generateTunnelId();
        }
        for (let i = 0;i<=createNameRetries; i++){
            try {
                const result = (await this.sendTunnelRequest<Tunnel>(
                    'PUT',
                    tunnel,
                    manageAccessTokenScope,
                    undefined,
                    undefined,
                    options,
                    this.convertTunnelForRequest(tunnel),
                    undefined,
                    cancellation,
                    true,
                ))!;
                preserveAccessTokens(tunnel, result);
                parseTunnelDates(result);
                return result;
            } catch (error) {
                if (idGenerated) {
                    // The tunnel ID was generated and there was a conflict.
                    // Try again with a new ID.
                    tunnel.tunnelId = IdGeneration.generateTunnelId();
                } else {
                    throw error;
                }
            }
        }

        const result2 = (await this.sendTunnelRequest<Tunnel>(
            'PUT',
            tunnel,
            manageAccessTokenScope,
            undefined,
            "forceCreate=true",
            options,
            this.convertTunnelForRequest(tunnel),
            undefined,
            cancellation,
            true,
        ))!;
        preserveAccessTokens(tunnel, result2);
        parseTunnelDates(result2);
        return result2;
    }

    public async updateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<Tunnel> {
        options = options || {};
        options.additionalHeaders = options.additionalHeaders || {};
        options.additionalHeaders['If-Match'] = "*";
        const result = (await this.sendTunnelRequest<Tunnel>(
            'PUT',
            tunnel,
            manageAccessTokenScope,
            undefined,
            undefined,
            options,
            this.convertTunnelForRequest(tunnel),
            undefined,
            cancellation,
        ))!;
        preserveAccessTokens(tunnel, result);
        parseTunnelDates(result);
        return result;
    }

    public async deleteTunnel(tunnel: Tunnel, options?: TunnelRequestOptions, cancellation?: CancellationToken): Promise<boolean> {
        return await this.sendTunnelRequest<boolean>(
            'DELETE',
            tunnel,
            manageAccessTokenScope,
            undefined,
            undefined,
            options,
            undefined,
            true,
            cancellation,
        );
    }

    public async updateTunnelEndpoint(
        tunnel: Tunnel,
        endpoint: TunnelEndpoint,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelEndpoint> {
        if (endpoint.id == null) {
            throw new Error('Endpoint ID must be specified when updating an endpoint.');
        }
        const path = `${endpointsApiSubPath}/${endpoint.id}`;
        const result = (await this.sendTunnelRequest<TunnelEndpoint>(
            'PUT',
            tunnel,
            hostAccessTokenScope,
            path,
            "connectionMode=" + endpoint.connectionMode,
            options,
            endpoint,
            undefined,
            cancellation,
        ))!;

        if (tunnel.endpoints) {
            // Also update the endpoint in the local tunnel object.
            tunnel.endpoints = tunnel.endpoints
                .filter(
                    (e) =>
                        e.hostId !== endpoint.hostId ||
                        e.connectionMode !== endpoint.connectionMode,
                )
                .concat(result);
        }

        return result;
    }

    public async deleteTunnelEndpoints(
        tunnel: Tunnel,
        id: string,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<boolean> {
        const path = `${endpointsApiSubPath}/${id}`;
        const result = await this.sendTunnelRequest<boolean>(
            'DELETE',
            tunnel,
            hostAccessTokenScope,
            path,
            undefined,
            options,
            undefined,
            true,
            cancellation,
        );

        if (result && tunnel.endpoints) {
            // Also delete the endpoint in the local tunnel object.
            tunnel.endpoints = tunnel.endpoints.filter(
                (e) => e.id !== id,
            );
        }

        return result;
    }

    public async listUserLimits(cancellation?: CancellationToken): Promise<NamedRateStatus[]> {
        const results = await this.sendRequest<NamedRateStatus[]>(
            'GET',
            undefined,
            limitsApiPath,
            undefined,
            undefined,
            undefined,
            undefined,
            cancellation,
        );
        return results || [];
    }

    public async listTunnelPorts(
        tunnel: Tunnel,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort[]> {
        const results = (await this.sendTunnelRequest<TunnelPortListResponse>(
            'GET',
            tunnel,
            readAccessTokenScopes,
            portsApiSubPath,
            undefined,
            options,
            undefined,
            undefined,
            cancellation,
        ))!;
        if (results.value){
            results.value.forEach(parseTunnelPortDates);
        }
        return results.value;
    }

    public async getTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort | null> {
        this.raiseReportProgress(TunnelProgress.StartingGetTunnelPort);
        const path = `${portsApiSubPath}/${portNumber}`;
        const result = await this.sendTunnelRequest<TunnelPort>(
            'GET',
            tunnel,
            readAccessTokenScopes,
            path,
            undefined,
            options,
            undefined,
            undefined,
            cancellation,
        );
        parseTunnelPortDates(result);
        this.raiseReportProgress(TunnelProgress.CompletedGetTunnelPort);
        return result;
    }

    public async createTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort> {
        this.raiseReportProgress(TunnelProgress.StartingCreateTunnelPort);
        tunnelPort = this.convertTunnelPortForRequest(tunnel, tunnelPort);
        const path = `${portsApiSubPath}/${tunnelPort.portNumber}`;
        options = options || {};
        options.additionalHeaders = options.additionalHeaders || {};
        options.additionalHeaders['If-Not-Match'] = "*";
        const result = (await this.sendTunnelRequest<TunnelPort>(
            'PUT',
            tunnel,
            managePortsAccessTokenScopes,
            path,
            undefined,
            options,
            tunnelPort,
            undefined,
            cancellation,
        ))!;
        
        tunnel.ports = tunnel.ports || [];
        // Also add the port to the local tunnel object.
        tunnel.ports = tunnel.ports
            .filter((p) => p.portNumber !== tunnelPort.portNumber)
            .concat(result)
            .sort(comparePorts);

        parseTunnelPortDates(result);
        this.raiseReportProgress(TunnelProgress.CompletedCreateTunnelPort);
        return result;
    }

    public async updateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort> {
        if (tunnelPort.clusterId && tunnel.clusterId && tunnelPort.clusterId !== tunnel.clusterId) {
            throw new Error('Tunnel port cluster ID is not consistent.');
        }

        options = options || {};
        options.additionalHeaders = options.additionalHeaders || {};
        options.additionalHeaders['If-Match'] = "*";
        const portNumber = tunnelPort.portNumber;
        const path = `${portsApiSubPath}/${portNumber}`;
        tunnelPort = this.convertTunnelPortForRequest(tunnel, tunnelPort);
        const result = (await this.sendTunnelRequest<TunnelPort>(
            'PUT',
            tunnel,
            managePortsAccessTokenScopes,
            path,
            undefined,
            options,
            tunnelPort,
            undefined,
            cancellation,
        ))!;
        preserveAccessTokens(tunnelPort, result);
        parseTunnelPortDates(result);

        tunnel.ports = tunnel.ports || [];
        // Also add the port to the local tunnel object.
        tunnel.ports = tunnel.ports
            .filter((p) => p.portNumber !== tunnelPort.portNumber)
            .concat(result)
            .sort(comparePorts);

        return result;
    }

    public async createOrUpdateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<TunnelPort> {
        tunnelPort = this.convertTunnelPortForRequest(tunnel, tunnelPort);
        const path = `${portsApiSubPath}/${tunnelPort.portNumber}`;
        const result = (await this.sendTunnelRequest<TunnelPort>(
            'PUT',
            tunnel,
            managePortsAccessTokenScopes,
            path,
            undefined,
            options,
            tunnelPort,
            undefined,
            cancellation,
        ))!;

        tunnel.ports = tunnel.ports || [];
        // Also add the port to the local tunnel object.
        tunnel.ports = tunnel.ports
            .filter((p) => p.portNumber !== tunnelPort.portNumber)
            .concat(result)
            .sort(comparePorts);

        parseTunnelPortDates(result);
        return result;
    }

    public async deleteTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
        cancellation?: CancellationToken,
    ): Promise<boolean> {
        const path = `${portsApiSubPath}/${portNumber}`;
        const result = await this.sendTunnelRequest<boolean>(
            'DELETE',
            tunnel,
            managePortsAccessTokenScopes,
            path,
            undefined,
            options,
            undefined,
            true,
            cancellation,
        );

        if (result && tunnel.ports) {
            // Also delete the port in the local tunnel object.
            tunnel.ports = tunnel.ports
                .filter((p) => p.portNumber !== portNumber)
                .sort(comparePorts);
        }

        return result;
    }

    public async listClusters(cancellation?: CancellationToken): Promise<ClusterDetails[]> {
        return (await this.sendRequest<ClusterDetails[]>(
            'GET',
            undefined,
            clustersApiPath,
            undefined,
            undefined,
            undefined,
            false,
            cancellation,
        ))!;
    }

    /**
     * Sends an HTTP request to the tunnel management API, targeting a specific tunnel.
     * This protected method enables subclasses to support additional tunnel management APIs.
     * @param method HTTP request method.
     * @param tunnel Tunnel that the request is targeting.
     * @param accessTokenScopes Required array of access scopes for tokens in `tunnel.accessTokens`
     * that could be used to authorize the request.
     * @param path Optional request sub-path relative to the tunnel.
     * @param query Optional query string to append to the request.
     * @param options Request options.
     * @param body Optional request body object.
     * @param allowNotFound If true, a 404 response is returned as a null or false result
     * instead of an error.
     * @param cancellationToken Optional cancellation token for the request.
     * @param isCreate Set to true if this is a tunnel create request, default is false.
     * @returns Result of the request.
     */
    protected async sendTunnelRequest<TResult>(
        method: Method,
        tunnel: Tunnel,
        accessTokenScopes: string[],
        path?: string,
        query?: string,
        options?: TunnelRequestOptions,
        body?: object,
        allowNotFound?: boolean,
        cancellation?: CancellationToken,
        isCreate: boolean = false
    ): Promise<NullableIfNotBoolean<TResult>> {
        this.raiseReportProgress(TunnelProgress.StartingRequestUri);
        const uri = await this.buildUriForTunnel(tunnel, path, query, options, isCreate);
        this.raiseReportProgress(TunnelProgress.StartingRequestConfig);
        const config = await this.getAxiosRequestConfig(tunnel, options, accessTokenScopes);
        this.raiseReportProgress(TunnelProgress.StartingSendTunnelRequest);
        try {
            const result = await this.request<TResult>(method, uri, body, config, allowNotFound, cancellation);
            this.raiseReportProgress(TunnelProgress.CompletedSendTunnelRequest);
            return result;
        } catch (error) {
            if (/certificate/i.test((error as AxiosError<any>).message)) {
                const originalErrorMessage = (error as AxiosError<any>).message;
                throw new Error("Tunnel service HTTPS certificate is invalid. This may be caused by the use of a " +
                    "self-signed certificate or a firewall intercepting the connection. " + originalErrorMessage + ". ");
            }
            throw error;
        }
    }

    /**
     * Sends an HTTP request to the tunnel management API.
     * This protected method enables subclasses to support additional tunnel management APIs.
     * @param method HTTP request method.
     * @param clusterId Optional tunnel service cluster ID to direct the request to. If unspecified,
     * the request will use the global traffic-manager to find the nearest cluster.
     * @param path Required request path.
     * @param query Optional query string to append to the request.
     * @param options Request options.
     * @param body Optional request body object.
     * @param allowNotFound If true, a 404 response is returned as a null or false result
     * instead of an error.
     * @param cancellationToken Optional cancellation token for the request.
     * @returns Result of the request.
     */
    protected async sendRequest<TResult>(
        method: Method,
        clusterId: string | undefined,
        path: string,
        query?: string,
        options?: TunnelRequestOptions,
        body?: object,
        allowNotFound?: boolean,
        cancellation?: CancellationToken,
    ): Promise<NullableIfNotBoolean<TResult>> {
        this.raiseReportProgress(TunnelProgress.StartingSendTunnelRequest);
        const uri = await this.buildUri(clusterId, path, query, options);
        const config = await this.getAxiosRequestConfig(undefined, options);
        try {
            const result = await this.request<TResult>(method, uri, body, config, allowNotFound, cancellation);
            this.raiseReportProgress(TunnelProgress.CompletedSendTunnelRequest);
            return result;
        } catch (error) {
            if (/certificate/i.test((error as Error).message)) {
                throw new Error("Tunnel service HTTPS certificate is invalid. This may be caused by the use of a "+
                "self signed certificate or a firewall intercepting the connection.");
            } 
            throw error;
        }
    }

    public async checkNameAvailablility(tunnelName: string, cancellation?: CancellationToken): Promise<boolean> {
        tunnelName = encodeURI(tunnelName);
        const uri = await this.buildUri(
            undefined,
            `${tunnelsApiPath}/${tunnelName}${checkAvailablePath}`,
        );
        const config: AxiosRequestConfig = {
            httpsAgent: this.httpsAgent,
            adapter: this.adapter,
        };
        return await this.request<boolean>('GET', uri, undefined, config, undefined, cancellation);
    }

    public reportEvent(tunnel: Tunnel, tunnelEvent: TunnelEvent, options?: TunnelRequestOptions): void {
        if (!tunnel) {
            throw new TypeError('A tunnel is required.');
        }
        if (!tunnelEvent) {
            throw new TypeError('A tunnelEvent is required.');
        }

        if (!this.apiVersion) {
            // Events are not supported by the V1 API.
            return;
        }

        if (!this.enableEventsReporting) {
            return;
        }

        if (this.isDisposed) {
            // Do not queue any more events after the client is disposed.
            return;
        }

        // Set the client timestamp if it wasn't already initialized.
        tunnelEvent.timestamp ??= new Date();

        const wasEmpty = this.eventsQueue.length === 0;
        this.eventsQueue.push({
            tunnel: tunnel,
            event: tunnelEvent,
            requestOptions: options
        });

        // Signal that events are available
        if (wasEmpty) {
            this.eventsAvailableCompletion.resolve();
        }

        if (this.eventsPromise === null) {
            this.eventsPromise = this.processPendingEventsAsync();
        }
    }

    private async processPendingEventsAsync(): Promise<void> {
        const eventsToSend: TunnelEvent[] = [];
        
        while (!this.isDisposed) {
            await this.eventsAvailableCompletion.promise;
            this.eventsAvailableCompletion = new PromiseCompletionSource<void>();

            // Get the first event
            const nextEventInfo = this.eventsQueue.shift();
            if (!nextEventInfo) {
                // The completion was resolved, but no events were queued.
                // This indicates the client is being disposed.
                break;
            }

            const tunnel = nextEventInfo.tunnel;
            const requestOptions = nextEventInfo.requestOptions;
            eventsToSend.length = 0;
            eventsToSend.push(nextEventInfo.event);

            // Batch events for the same tunnel with the same request options
            while (this.eventsQueue.length > 0) {
                const peekEventInfo = this.eventsQueue[0];
                
                // Check if next event is for the same tunnel and has same request options
                if (peekEventInfo.tunnel !== tunnel || peekEventInfo.requestOptions !== requestOptions) {
                    // Different tunnel or options, process as separate batch
                    break;
                }

                eventsToSend.push(this.eventsQueue.shift()!.event);
            }

            // Upload a batch of events for the same tunnel
            try {
                // Do not use sendTunnelRequest() here, to avoid reporting progress
                // for these requests.
                const uri = await this.buildUriForTunnel(
                    tunnel,
                    eventsApiSubPath,
                    this.tunnelRequestOptionsToQueryString(requestOptions),
                    requestOptions
                );
                const config = await this.getAxiosRequestConfig(
                    tunnel,
                    requestOptions,
                    readAccessTokenScopes
                );
                await this.request<boolean>(
                    'POST',
                    uri,
                    [...eventsToSend], // Create a copy to avoid mutation issues
                    config,
                    undefined,
                    undefined
                );
            } catch (error) {
                // Errors uploading events are ignored.
                this.trace(`Error uploading events: ${error}`);
            }
        }
    }

    private raiseReportProgress(progress: TunnelProgress) {
        const args : TunnelReportProgressEventArgs = {
            progress: progress
        }
        this.reportProgressEmitter.fire(args);
    }

    private getResponseErrorMessage(error: AxiosError, signal: AbortSignal) {
        let errorMessage = '';

        if (signal.aborted) {
            // connection timeout
            error.code = 'ECONNABORTED';
            errorMessage = `ECONNABORTED: (signal aborted) ${error.message}`
        } else if (error.code === 'ECONNABORTED') {
            // server timeout
            errorMessage = `ECONNABORTED: (timeout) ${error.message}`;
        }

        if (error.response?.data) {
            const problemDetails: ProblemDetails = error.response.data;
            if (problemDetails.title || problemDetails.detail) {
                errorMessage = `Tunnel service error: ${problemDetails.title}`;
                if (problemDetails.detail) {
                    errorMessage += ' ' + problemDetails.detail;
                }
                if (problemDetails.errors) {
                    errorMessage += JSON.stringify(problemDetails.errors);
                }
            }
        }

        if (!errorMessage && error.response && error.response.status &&
            error.response.status >= 400 && error.response.status < 500 &&
            error.response.headers
        ) {
            const headers = error.response.headers;
            const servedBy = headers['X-Served-By'] || headers['x-served-by'];
            if (!/tunnels-/.test(servedBy)) {
                // The response did not include either a ProblemDetails body object or a header
                // confirming it was served by the tunnel service. This check excludes 5xx status
                // responses which may include non-firwall network infrastructure issues.
                const requestDomain = new URL(error.config?.url ??
                    TunnelServiceProperties.production.serviceUri).host;
                errorMessage = 'The tunnel request resulted in ' +
                    `${error.response.status} status, but the request ` +
                    'did not reach the tunnel service. This may indicate the domain ' +
                    `'${requestDomain}' is blocked by a firewall.`;
            }
        }

        if (!errorMessage) {
            if (error.response) {
                errorMessage =
                    'Tunnel service returned status code: ' +
                    `${error.response.status} ${error.response.statusText}`;
            } else {
                errorMessage = error?.message ?? error ?? 'Unknown tunnel service request error.';
            }
        }

        const requestIdHeaderName = 'VsSaaS-Request-Id';
        if (error.response?.headers && error.response.headers[requestIdHeaderName]) {
            errorMessage += `\nRequest ID: ${error.response.headers[requestIdHeaderName]}`;
        }

        return errorMessage;
    }

    // Helper functions
    private async buildUri(
        clusterId: string | undefined,
        path: string,
        query?: string,
        options?: TunnelRequestOptions,
    ) {
        if (clusterId === undefined && this.userTokenCallback) {
            let token = await this.userTokenCallback();
            if (token && token.startsWith("tunnelplan")) {
                token = token.replace("tunnelplan ", "");
                const parsedToken = TunnelPlanTokenProperties.tryParse(token)
                if (parsedToken !== null && parsedToken.clusterId) {
                    clusterId = parsedToken.clusterId
                }
            }
        }
        let baseAddress = this.baseAddress;
        if (clusterId) {
            const url = new URL(baseAddress);
            const portNumber = parseInt(url.port, 10);

            url.hostname = TunnelManagementHttpClient.replaceTunnelServiceHostnameClusterId(
                url.hostname, clusterId);

            if (
                url.protocol === 'https:' &&
                clusterId.startsWith('localhost') &&
                portNumber % 10 > 0
            ) {
                // Local testing simulates clusters by running the service on multiple ports.
                // Change the port number to match the cluster ID suffix.
                const clusterNumber = parseInt(clusterId.substring('localhost'.length), 10);
                if (clusterNumber > 0 && clusterNumber < 10) {
                    url.port = (portNumber - (portNumber % 10) + clusterNumber).toString();
                }
            }

            baseAddress = url.toString();
        }

        baseAddress = `${baseAddress.replace(/\/$/, '')}${path}`;

        const optionsQuery = this.tunnelRequestOptionsToQueryString(options, query);
        if (optionsQuery) {
            baseAddress += `?${optionsQuery}`;
        }

        return baseAddress;
    }

    private static replaceTunnelServiceHostnameClusterId(
        hostname: string,
        clusterId: string | undefined,
    ): string {
        // tunnels.local.api.visualstudio.com resolves to localhost (for local development).
        if (!clusterId ||
            hostname === 'localhost' ||
            hostname === 'tunnels.local.api.visualstudio.com'
        ) {
            return hostname;
        }

        if (hostname.startsWith('global.') ||
            TunnelConstraints.clusterIdPrefixRegex.test(hostname)) {
            // Hostname is in the form "global.rel.tunnels..." or "<clusterId>.rel.tunnels..."
            // Replace the first part of the hostname with the specified cluster ID.
            return clusterId + hostname.substring(hostname.indexOf('.'));
        } else {
            // Hostname does not have a recognized cluster prefix. Prepend the cluster ID.
            return `${clusterId}.${hostname}`;
        }
    }

    private buildUriForTunnel(
        tunnel: Tunnel,
        path?: string,
        query?: string,
        options?: TunnelRequestOptions,
        isCreate: boolean = false,
    ) {
        let tunnelPath = '';
        if ((tunnel.clusterId || isCreate) && tunnel.tunnelId) {
            tunnelPath = `${tunnelsApiPath}/${tunnel.tunnelId}`;
        } else {
              throw new Error(
                  'Tunnel object must include a tunnel ID always and cluster ID for non creates.',
              );
        }

        if (options?.additionalQueryParameters) {
            for (const [paramName, paramValue] of Object.entries(options.additionalQueryParameters)) {
                if (query) {
                    query += `&${paramName}=${paramValue}`;
                } else {
                    query = `${paramName}=${paramValue}`;
                }
            }
        }

        return this.buildUri(tunnel.clusterId, tunnelPath + (path ? path : ''), query, options);
    }

    private async getAxiosRequestConfig(
        tunnel?: Tunnel,
        options?: TunnelRequestOptions,
        accessTokenScopes?: string[],
    ): Promise<AxiosRequestConfig> {
        // Get access token header
        const headers: { [name: string]: string } = {};

        if (options && options.accessToken) {
            headers[
                tunnelAuthentication
            ] = `${TunnelAuthenticationSchemes.tunnel} ${options.accessToken}`;
        }

        if (!(tunnelAuthentication in headers) && this.userTokenCallback) {
            const token = await this.userTokenCallback();
            if (token) {
                headers[tunnelAuthentication] = token;
            }
        }

        if (!(tunnelAuthentication in headers)) {
            const accessToken = TunnelAccessTokenProperties.getTunnelAccessToken(
                tunnel,
                accessTokenScopes,
            );
            if (accessToken) {
                headers[
                    tunnelAuthentication
                ] = `${TunnelAuthenticationSchemes.tunnel} ${accessToken}`;
            }
        }

        const copyAdditionalHeaders = (additionalHeaders?: { [name: string]: string }) => {
            if (additionalHeaders) {
                for (const [headerName, headerValue] of Object.entries(additionalHeaders)) {
                    headers[headerName] = headerValue;
                }
            }
        };
        copyAdditionalHeaders(this.additionalRequestHeaders);
        copyAdditionalHeaders(options?.additionalHeaders);

        const userAgentPrefix = headers['User-Agent'] ? headers['User-Agent'] + ' ' : '';

        headers['User-Agent'] = `${userAgentPrefix}${this.userAgents} ${tunnelSdkUserAgent}`;

        // Get axios config
        const config: AxiosRequestConfig = {
            headers,
            ...(this.httpsAgent && { httpsAgent: this.httpsAgent }),
            ...(this.adapter && { adapter: this.adapter }),
        };

        if (options?.followRedirects === false) {
            config.maxRedirects = 0;
        }

        return config;
    }

    private convertTunnelForRequest(tunnel: Tunnel): Tunnel {
        const convertedTunnel: Tunnel = {
            tunnelId: tunnel.tunnelId,
            name: tunnel.name,
            domain: tunnel.domain,
            description: tunnel.description,
            labels: tunnel.labels,
            options: tunnel.options,
            customExpiration: tunnel.customExpiration,
            accessControl: !tunnel.accessControl
                ? undefined
                : { entries: tunnel.accessControl.entries.filter((ace) => !ace.isInherited) },
            endpoints: tunnel.endpoints,
            ports: tunnel.ports?.map((p) => this.convertTunnelPortForRequest(tunnel, p)),
        };

        return convertedTunnel;
    }

    private convertTunnelPortForRequest(tunnel: Tunnel, tunnelPort: TunnelPort): TunnelPort {
        if (tunnelPort.clusterId && tunnel.clusterId && tunnelPort.clusterId !== tunnel.clusterId) {
            throw new Error('Tunnel port cluster ID does not match tunnel.');
        }

        if (tunnelPort.tunnelId && tunnel.tunnelId && tunnelPort.tunnelId !== tunnel.tunnelId) {
            throw new Error('Tunnel port tunnel ID does not match tunnel.');
        }

        return {
            portNumber: tunnelPort.portNumber,
            protocol: tunnelPort.protocol,
            isDefault: tunnelPort.isDefault,
            description: tunnelPort.description,
            labels: tunnelPort.labels,
            sshUser: tunnelPort.sshUser,
            options: tunnelPort.options,
            accessControl: !tunnelPort.accessControl
                ? undefined
                : { entries: tunnelPort.accessControl.entries.filter((ace) => !ace.isInherited) },
        };
    }

    private tunnelRequestOptionsToQueryString(
        options?: TunnelRequestOptions,
        additionalQuery?: string,
    ) {
        const queryOptions: { [name: string]: string[] } = {};
        const queryItems = [];

        if (options) {
            if (options.includePorts) {
                queryOptions.includePorts = ['true'];
            }

            if (options.includeAccessControl) {
                queryOptions.includeAccessControl = ['true'];
            }

            if (options.tokenScopes) {
                TunnelAccessControl.validateScopes(options.tokenScopes, undefined, true);
                queryOptions.tokenScopes = options.tokenScopes;
            }

            if (options.forceRename) {
                queryOptions.forceRename = ['true'];
            }

            if (options.labels) {
                queryOptions.labels = options.labels;
                if (options.requireAllLabels) {
                    queryOptions.allLabels = ['true'];
                }
            }

            if (options.limit) {
                queryOptions.limit = [options.limit.toString()];
            }

            queryItems.push(
                ...Object.keys(queryOptions).map((key) => {
                    const value = queryOptions[key];
                    return `${key}=${value.map(encodeURIComponent).join(',')}`;
                }),
            );
        }

        if (additionalQuery) {
            queryItems.push(additionalQuery);
        }
        queryItems.push(`api-version=${this.apiVersion}`)

        const queryString = queryItems.join('&');
        return queryString;
    }

    /**
     * Axios request that can be overridden for unit tests purposes.
     * @param config axios request config
     * @param _cancellation the cancellation token for the request (used by unit tests to simulate timeouts).
     */
    private async axiosRequest<TResponse>(config: AxiosRequestConfig, _cancellation?: CancellationToken) {
        return await axios.request<TResponse>(config);
    }

    /**
     * Makes an HTTP request using Axios, while tracing request and response details.
     */
    private async request<TResult>(
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
        allowNotFound?: boolean,
        cancellation?: CancellationToken,
    ): Promise<NullableIfNotBoolean<TResult>> {
        this.trace(`${method} ${uri}`);
        if (config.headers) {
            this.traceHeaders(config.headers);
        }
        this.traceContent(data);

        const traceResponse = (response: AxiosResponse) => {
            this.trace(`${response.status} ${response.statusText}`);
            this.traceHeaders(response.headers);
            this.traceContent(response.data);
        };

        let disposable: Disposable | undefined;
        const abortController = new AbortController();
        let timeout: NodeJS.Timeout | undefined = undefined;
        const newAbortSignal = () => {
            if (cancellation?.isCancellationRequested) {
                abortController.abort('Cancelled: CancellationToken cancel requested.');
            } else if (cancellation) {
                disposable = cancellation.onCancellationRequested(() => abortController.abort('Cancelled: CancellationToken cancel requested.'));
            } else {
                timeout = setTimeout(() => abortController.abort('Cancelled: default request timeout reached.'), defaultRequestTimeoutMS);
            }
            return abortController.signal;
        }

        try {
            config.url = uri;
            config.method = method;
            config.data = data;
            config.signal = newAbortSignal();
            config.timeout = defaultRequestTimeoutMS;

            const response = await this.axiosRequest<TResult>(config, cancellation);
            traceResponse(response);

            // This assumes that TResult is always boolean for DELETE requests.
            return <NullableIfNotBoolean<TResult>>(method === 'DELETE' ? true : response.data);
        } catch (e) {
            if (!(e instanceof Error) || !(e as AxiosError).isAxiosError) throw e;
            const requestError = e as AxiosError;
            if (requestError.response) {
                traceResponse(requestError.response);

                if (allowNotFound && requestError.response.status === 404) {
                    return <NullableIfNotBoolean<TResult>>(method === 'DELETE' ? false : null);
                }
            }

            requestError.message = this.getResponseErrorMessage(requestError, abortController.signal);

            // Axios errors have too much redundant detail! Delete some of it.
            delete requestError.request;
            if (requestError.response) {
                delete requestError.config?.httpAgent;
                delete requestError.config?.httpsAgent;
                delete requestError.response.request;
            }

            throw requestError;
        } finally {
            if (timeout) {
                clearTimeout(timeout);
            }
            disposable?.dispose();
        }
    }

    private traceHeaders(headers: { [key: string]: unknown }): void {
        for (const [headerName, headerValue] of Object.entries(headers)) {
            if (headerName === 'Authorization') {
                this.traceAuthorizationHeader(headerName, headerValue as string);
                return;
            }

            this.trace(`${headerName}: ${headerValue ?? ''}`);
        }
    }

    private traceAuthorizationHeader(key: string, value: string): void {
        if (typeof value !== 'string') return;

        const spaceIndex = value.indexOf(' ');
        if (spaceIndex < 0) {
            this.trace(`${key}: [${value.length}]`);
            return;
        }

        const scheme = value.substring(0, spaceIndex);
        const token = value.substring(spaceIndex + 1);

        if (scheme.toLowerCase() === TunnelAuthenticationSchemes.tunnel.toLowerCase()) {
            const tokenProperties = TunnelAccessTokenProperties.tryParse(token);
            if (tokenProperties) {
                this.trace(`${key}: ${scheme} <${tokenProperties}>`);
                return;
            }
        }

        this.trace(`${key}: ${scheme} <token>`);
    }

    private traceContent(data: any) {
        if (typeof data === 'object') {
            data = JSON.stringify(data, undefined, '  ');
        }

        if (typeof data === 'string') {
            this.trace(TunnelManagementHttpClient.replaceTokensInContent(data));
        }
    }

    private static replaceTokensInContent(content: string): string {
        const tokenRegex = /"(eyJ[a-zA-z0-9\-_]+\.[a-zA-z0-9\-_]+\.[a-zA-z0-9\-_]+)"/;

        let match = tokenRegex.exec(content);
        while (match) {
            let token = match[1];
            const tokenProperties = TunnelAccessTokenProperties.tryParse(token);
            token = tokenProperties?.toString() ?? 'token';
            content =
                content.substring(0, match.index + 1) +
                '<' +
                token +
                '>' +
                content.substring(match.index + match[0].length - 1);
            match = tokenRegex.exec(content);
        }

        return content;
    }

    /**
     * Disposes the client and any background tasks.
     */
    public async dispose(): Promise<void> {
        this.isDisposed = true;
        
        // Resolving the events-available completion will cause the events processing task
        // to exit after processing any remaining already-queued events.
        this.eventsAvailableCompletion.resolve();
        
        if (this.eventsPromise) {
            await this.eventsPromise;
            this.eventsPromise = null;
        }
    }
}