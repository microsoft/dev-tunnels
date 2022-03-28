import {
    Tunnel,
    TunnelConnectionMode,
    TunnelAccessScopes,
    TunnelEndpoint,
    TunnelPort,
    ProblemDetails,
    TunnelServiceProperties,
} from '@vs/tunnels-contracts';
import {
    ProductHeaderValue,
    TunnelAuthenticationSchemes,
    TunnelManagementClient,
} from './tunnelManagementClient';
import { TunnelRequestOptions } from './tunnelRequestOptions';
import { tunnelSdkUserAgent } from './version';
import axios, { AxiosError, AxiosRequestConfig, AxiosResponse, Method } from 'axios';
import * as https from 'https';
import { SshAuthenticationType } from '@vs/vs-ssh';

const tunnelsApiPath = '/api/v1/tunnels';
const endpointsApiSubPath = '/endpoints';
const portsApiSubPath = '/ports';
const tunnelAuthentication = 'Authorization';

function comparePorts(a: TunnelPort, b: TunnelPort) {
    return (a.portNumber ?? Number.MAX_SAFE_INTEGER) - (b.portNumber ?? Number.MAX_SAFE_INTEGER);
}

const manageAccessTokenScope = [TunnelAccessScopes.manage];
const hostAccessTokenScope = [TunnelAccessScopes.host];
const hostOrManageAccessTokenScopes = [TunnelAccessScopes.manage, TunnelAccessScopes.host];
const readAccessTokenScopes = [
    TunnelAccessScopes.manage,
    TunnelAccessScopes.host,
    TunnelAccessScopes.connect,
];

export class TunnelManagementHttpClient implements TunnelManagementClient {
    public additionalRequestHeaders?: { [header: string]: string };

    private readonly baseAddress: string;
    private readonly accessTokenCallback: () => Promise<string | null>;

    public trace: (msg: string) => void = (msg) => { };

    /**
     * Initializes a new instance of the `TunnelManagementHttpClient` class
     * with a client authentication callback, service URI, and HTTP handler.
     *
     * @param userAgent { name, version } object or a comment string to use as the User-Agent header.
     * @param accessTokenCallback Optional async callback for retrieving a client authentication
     * header value with access token, for AAD or GitHub user authentication. This may be omitted
     * for anonymous tunnel clients, or if tunnel access tokens will be specified via
     * `TunnelRequestOptions.accessToken`.
     * @param tunnelServiceUri Optional tunnel service URI (not including any path). Defaults to
     * the global tunnel service URI.
     * @param httpsAgent Optional agent that will be invoked for HTTPS requests to the tunnel
     * service.
     */
    constructor(
        private userAgent: ProductHeaderValue[] | ProductHeaderValue | string,
        accessTokenCallback?: () => Promise<string | null>,
        tunnelServiceUri?: string,
        public readonly httpsAgent?: https.Agent,
    ) {
        if (!userAgent) {
            throw new TypeError('User agent must be provided.');
        }

        if (Array.isArray(userAgent)) {
            userAgent.forEach(userAgent => {
                if (!userAgent.name) {
                    throw new TypeError('Invalid user agent. The name must be provided.');
                }

                if (typeof userAgent.name !== 'string') {
                    throw new TypeError('Invalid user agent. The name must be a string.');
                }

                if (userAgent.version && typeof userAgent.version !== 'string') {
                    throw new TypeError('Invalid user agent. The version must be a string.');
                }
            })
        }
        else if (typeof userAgent !== 'string') {
            if (!userAgent.name) {
                throw new TypeError('Invalid user agent. The name must be provided.');
            }

            if (typeof userAgent.name !== 'string') {
                throw new TypeError('Invalid user agent. The name must be a string.');
            }

            if (userAgent.version && typeof userAgent.version !== 'string') {
                throw new TypeError('Invalid user agent. The version must be a string.');
            }
        }

        this.accessTokenCallback = accessTokenCallback ?? (() => Promise.resolve(null));

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
        options?: TunnelRequestOptions,
    ): Promise<Tunnel[]> {
        const query = clusterId ? 'global=true' : undefined;
        const uri = this.buildUri(clusterId, tunnelsApiPath, options, query);

        const config = await this.getAxiosRequestConfig(undefined, options, readAccessTokenScopes);
        return await this.request<Tunnel[]>('GET', uri, undefined, config);
    }

    public async searchTunnels(
        tags: string[],
        requireAllTags: boolean,
        clusterId?: string,
        options?: TunnelRequestOptions,
    ): Promise<Tunnel[]> {
        let query = clusterId ? 'global=true' : null;
        query = !query ? `allTags=${requireAllTags}` : `${query}&allTags=${requireAllTags}`;
        let tagsString = tags.map(encodeURI).join(',');
        query += `&tags=${tagsString}`;

        const uri = this.buildUri(clusterId, tunnelsApiPath, options, query);

        const config = await this.getAxiosRequestConfig(undefined, options, readAccessTokenScopes);
        return await this.request<Tunnel[]>('GET', uri, undefined, config);
    }

    public async getTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel | null> {
        const uri = this.buildUriForTunnel(tunnel, options);

        const config = await this.getAxiosRequestConfig(tunnel, options, readAccessTokenScopes);
        return await this.request<Tunnel | null>('GET', uri, undefined, config);
    }

    public async createTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel> {
        let tunnelId = tunnel.tunnelId;
        if (tunnelId) {
            throw new Error('An ID may not be specified when creating a tunnel.');
        }

        const uri = this.buildUri(tunnel.clusterId, tunnelsApiPath, options);

        const config = await this.getAxiosRequestConfig(tunnel, options, manageAccessTokenScope);
        tunnel = this.convertTunnelForRequest(tunnel);
        return await this.request<Tunnel>('POST', uri, tunnel, config);
    }

    public async updateTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<Tunnel> {
        const uri = this.buildUriForTunnel(tunnel, options);

        const config = await this.getAxiosRequestConfig(tunnel, options, manageAccessTokenScope);
        const result = await this.request<Tunnel>(
            'PUT',
            uri,
            this.convertTunnelForRequest(tunnel),
            config,
        );

        if (!options?.tokenScopes) {
            // If no new tokens were requested in the update, preserve any existing
            // access tokens in the resulting tunnel object.
            result.accessTokens = tunnel.accessTokens;
        }

        return result;
    }

    public async deleteTunnel(tunnel: Tunnel, options?: TunnelRequestOptions): Promise<boolean> {
        const uri = this.buildUriForTunnel(tunnel, options);

        const config = await this.getAxiosRequestConfig(tunnel, options, manageAccessTokenScope);
        return await this.request<boolean>('DELETE', uri, undefined, config);
    }

    public async updateTunnelEndpoint(
        tunnel: Tunnel,
        endpoint: TunnelEndpoint,
        options?: TunnelRequestOptions,
    ): Promise<TunnelEndpoint> {
        const uri = this.buildUriForTunnel(
            tunnel,
            options,
            `${endpointsApiSubPath}/${endpoint.hostId}/${endpoint.connectionMode}`,
        );

        const config = await this.getAxiosRequestConfig(tunnel, options, hostAccessTokenScope);
        const result = await this.request<TunnelEndpoint>('PUT', uri, endpoint, config);

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
        hostId: string,
        connectionMode?: TunnelConnectionMode,
        options?: TunnelRequestOptions,
    ): Promise<boolean> {
        let path =
            connectionMode == null
                ? `${endpointsApiSubPath}/${hostId}`
                : `${endpointsApiSubPath}/${hostId}/${connectionMode}`;

        const uri = this.buildUriForTunnel(tunnel, options, path);

        const config = await this.getAxiosRequestConfig(tunnel, options, hostAccessTokenScope);
        const result = await this.request<boolean>('DELETE', uri, undefined, config);

        if (result && tunnel.endpoints) {
            // Also delete the endpoint in the local tunnel object.
            tunnel.endpoints = tunnel.endpoints.filter(
                (e) => e.hostId !== hostId || e.connectionMode !== connectionMode,
            );
        }

        return result;
    }

    public async listTunnelPorts(
        tunnel: Tunnel,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort[]> {
        const uri = this.buildUriForTunnel(tunnel, options, portsApiSubPath);

        const config = await this.getAxiosRequestConfig(tunnel, options, readAccessTokenScopes);
        return await this.request<TunnelPort[]>('GET', uri, undefined, config);
    }

    public async getTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort> {
        const uri = this.buildUriForTunnel(tunnel, options, `${portsApiSubPath}/${portNumber}`);

        const config = await this.getAxiosRequestConfig(tunnel, options, readAccessTokenScopes);
        return await this.request<TunnelPort>('GET', uri, undefined, config);
    }

    public async createTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort> {
        const uri = this.buildUriForTunnel(tunnel, options, portsApiSubPath);

        const config = await this.getAxiosRequestConfig(
            tunnel,
            options,
            hostOrManageAccessTokenScopes,
        );
        tunnelPort = this.convertTunnelPortForRequest(tunnel, tunnelPort);
        const result = await this.request<TunnelPort>('POST', uri, tunnelPort, config);

        if (tunnel.ports) {
            // Also add the port to the local tunnel object.
            tunnel.ports = tunnel.ports
                .filter((p) => p.portNumber !== tunnelPort.portNumber)
                .concat(result)
                .sort(comparePorts);
        }

        return result;
    }

    public async updateTunnelPort(
        tunnel: Tunnel,
        tunnelPort: TunnelPort,
        options?: TunnelRequestOptions,
    ): Promise<TunnelPort> {
        if (tunnelPort.clusterId && tunnel.clusterId && tunnelPort.clusterId !== tunnel.clusterId) {
            throw new Error('Tunnel port cluster ID is not consistent.');
        }
        let portNumber = tunnelPort.portNumber;

        const uri = this.buildUriForTunnel(tunnel, options, `${portsApiSubPath}/${portNumber}`);

        const config = await this.getAxiosRequestConfig(
            tunnel,
            options,
            hostOrManageAccessTokenScopes,
        );
        const result = await this.request<TunnelPort>(
            'PUT',
            uri,
            this.convertTunnelPortForRequest(tunnel, tunnelPort),
            config,
        );

        if (tunnel.ports) {
            // Also update the port in the local tunnel object.
            tunnel.ports = tunnel.ports
                .filter((p) => p.portNumber !== tunnelPort.portNumber)
                .concat(result)
                .sort(comparePorts);
        }

        if (!options?.tokenScopes) {
            // If no new tokens were requested in the update, preserve any existing
            // access tokens in the resulting port object.
            result.accessTokens = tunnelPort.accessTokens;
        }

        return result;
    }

    public async deleteTunnelPort(
        tunnel: Tunnel,
        portNumber: number,
        options?: TunnelRequestOptions,
    ): Promise<boolean> {
        const uri = this.buildUriForTunnel(tunnel, options, `${portsApiSubPath}/${portNumber}`);
        const config = await this.getAxiosRequestConfig(
            tunnel,
            options,
            hostOrManageAccessTokenScopes,
        );
        const result = await this.request<boolean>('DELETE', uri, undefined, config);

        if (result && tunnel.ports) {
            // Also delete the port in the local tunnel object.
            tunnel.ports = tunnel.ports
                .filter((p) => p.portNumber !== portNumber)
                .sort(comparePorts);
        }

        return result;
    }

    private getResponseErrorMessage(error: AxiosError) {
        let errorMessage = '';
        if (error.response?.data) {
            let problemDetails: ProblemDetails = error.response.data;
            if (problemDetails.title || problemDetails.detail) {
                errorMessage = `Tunnel service error: ${problemDetails.title}`;
                if (problemDetails.detail) {
                    errorMessage += ' ' + problemDetails.detail;
                }
                if (problemDetails.error) {
                    errorMessage += JSON.stringify(problemDetails.error);
                }
            }
        }

        if (!errorMessage) {
            if (error?.response) {
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
    private buildUri(
        clusterId?: string,
        path?: string,
        options?: TunnelRequestOptions,
        query?: string,
    ) {
        let baseAddress = this.baseAddress;

        if (clusterId) {
            const url = new URL(baseAddress);
            const portNumber = parseInt(url.port, 10);
            if (url.hostname !== 'localhost' && !url.hostname.startsWith(`${clusterId}.`)) {
                // A specific cluster ID was specified (while not running on localhost).
                // Prepend the cluster ID to the hostname, and optionally strip a global prefix.
                url.hostname = `${clusterId}.${url.hostname}`.replace('global.', '');
                baseAddress = url.toString();
            } else if (
                url.protocol === 'https:' &&
                clusterId.startsWith('localhost') &&
                portNumber % 10 > 0
            ) {
                // Local testing simulates clusters by running the service on multiple ports.
                // Change the port number to match the cluster ID suffix.
                const clusterNumber = parseInt(clusterId.substring('localhost'.length), 10);
                if (clusterNumber > 0 && clusterNumber < 10) {
                    url.port = (portNumber - (portNumber % 10) + clusterNumber).toString();
                    baseAddress = url.toString();
                }
            }
        }

        baseAddress = `${baseAddress.replace(/\/$/, '')}${path}`;

        if (options) {
            let optionsQuery = this.tunnelRequestOptionsToQueryString(options);
            if (optionsQuery) {
                baseAddress += `?${optionsQuery}`;
            }
        }

        return baseAddress;
    }

    private buildUriForTunnel(
        tunnel: Tunnel,
        options?: TunnelRequestOptions,
        path?: string,
        query?: string,
    ) {
        let tunnelPath = '';
        if (tunnel.clusterId && tunnel.tunnelId) {
            tunnelPath = `${tunnelsApiPath}/${tunnel.tunnelId}`;
        } else {
            if (!tunnel.name) {
                throw new Error(
                    'Tunnel object must include either a name or tunnel ID and cluster ID.',
                );
            }
            tunnelPath = `${tunnelsApiPath}/${tunnel.name}`;
        }

        return this.buildUri(tunnel.clusterId, tunnelPath + (path ? path : ''), options, query);
    }

    private async getAxiosRequestConfig(
        tunnel?: Tunnel,
        options?: TunnelRequestOptions,
        scopes?: string[],
    ): Promise<AxiosRequestConfig> {
        // Get access token header
        let headers: { [name: string]: string } = {};

        if (options && options.accessToken) {
            headers[
                tunnelAuthentication
            ] = `${TunnelAuthenticationSchemes.tunnel} ${options.accessToken}`;
        }

        if (!(tunnelAuthentication in headers) && this.accessTokenCallback) {
            let token = await this.accessTokenCallback();
            if (token) {
                headers[tunnelAuthentication] = token;
            }
        }

        if (!(tunnelAuthentication in headers) && tunnel?.accessTokens) {
            for (let scope of scopes ?? []) {
                if (tunnel.accessTokens[scope]) {
                    headers[
                        tunnelAuthentication
                    ] = `${TunnelAuthenticationSchemes.tunnel} ${tunnel.accessTokens[scope]}`;
                    break;
                }
            }
        }

        const copyAdditionalHeaders = (additionalHeaders?: { [name: string]: string }) => {
            if (additionalHeaders) {
                for (let [headerName, headerValue] of Object.entries(additionalHeaders)) {
                    headers[headerName] = headerValue;
                }
            }
        };
        copyAdditionalHeaders(this.additionalRequestHeaders);
        copyAdditionalHeaders(options?.additionalHeaders);

        const userAgentPrefix = headers['User-Agent'] ? headers['User-Agent'] + ' ' : '';
        if (Array.isArray(this.userAgent)) {
            var combinedUserAgents = "";
            this.userAgent.forEach(userAgent => {
                combinedUserAgents = `${combinedUserAgents}${userAgent.name}/${userAgent.version ?? 'unknown'} `
            })
            this.userAgent = combinedUserAgents.trim()
        }
        const userAgentString =
            typeof this.userAgent === 'string'
                ? this.userAgent
                : `${this.userAgent.name}/${this.userAgent.version ?? 'unknown'}`;
        headers['User-Agent'] = `${userAgentPrefix}${userAgentString} ${tunnelSdkUserAgent}`;

        // Get axios config
        let config: AxiosRequestConfig = {
            headers,
            ...(this.httpsAgent && { httpsAgent: this.httpsAgent }),
        };

        if (options?.followRedirects === false) {
            config.maxRedirects = 0;
        }

        return config;
    }

    private convertTunnelForRequest(tunnel: Tunnel): Tunnel {
        if (tunnel.accessControl && tunnel.accessControl.entries.some((ace) => ace.isInherited)) {
            throw new Error('Tunnel access control cannot include inherited entries.');
        }

        const convertedTunnel: Tunnel = {
            name: tunnel.name,
            domain: tunnel.domain,
            description: tunnel.description,
            tags: tunnel.tags,
            options: tunnel.options,
            accessControl: tunnel.accessControl,
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
            options: tunnelPort.options,
            accessControl: !tunnelPort.accessControl
                ? undefined
                : { entries: tunnelPort.accessControl.entries.filter((ace) => !ace.isInherited) },
        };
    }

    private tunnelRequestOptionsToQueryString(options: TunnelRequestOptions) {
        let queryOptions: any = {};

        if (options.includePorts) {
            queryOptions['includePorts'] = 'true';
        }

        if (options.scopes) {
            TunnelAccessScopes.validate(options.scopes);
            queryOptions['scopes'] = options.scopes.join(',');
        }

        if (options.tokenScopes) {
            TunnelAccessScopes.validate(options.tokenScopes);
            queryOptions['tokenScopes'] = options.tokenScopes.join(',');
        }

        const queryString = Object.keys(queryOptions)
            .map((key) => {
                const value = queryOptions[key];

                return `${key}=${encodeURI(value)}`;
            })
            .join('&');

        return queryString;
    }

    /**
     * Makes an HTTP request using Axios, while tracing request and response details.
     */
    private async request<TResponse>(
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
    ): Promise<TResponse> {
        this.trace(`${method} ${uri}`);
        this.traceHeaders(config.headers);
        this.traceContent(data);

        const traceResponse = (response: AxiosResponse) => {
            this.trace(`${response.status} ${response.statusText}`);
            this.traceHeaders(response.headers);
            this.traceContent(response.data);
        };

        try {
            config.url = uri;
            config.method = method;
            config.data = data;

            let response = await axios.request<TResponse>(config);
            traceResponse(response);
            return response.data;
        } catch (e) {
            if (!(e instanceof Error) || !(e as AxiosError).isAxiosError) throw e;
            const requestError = e as AxiosError;
            if (requestError.response) traceResponse(requestError.response);
            requestError.message = this.getResponseErrorMessage(requestError);

            // Axios errors have too much redundant detail! Delete some of it.
            delete requestError.request;
            if (requestError.response) {
                delete requestError.config.httpAgent;
                delete requestError.config.httpsAgent;
                delete requestError.response.request;
            }

            throw requestError;
        }
    }

    private traceHeaders(headers: { [key: string]: unknown }): void {
        for (let [headerName, headerValue] of Object.entries(headers)) {
            this.trace(`${headerName}: ${headerValue ?? ''}`);
        }
    }

    private traceContent(data: any) {
        if (typeof data === 'object') {
            this.trace(JSON.stringify(data, undefined, '  '));
        } else if (typeof data === 'string') {
            this.trace(data);
        }
    }
}
