// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import axios, { Axios, AxiosHeaders, AxiosError, AxiosPromise, AxiosRequestConfig, AxiosResponse, Method } from 'axios';
import * as https from 'https';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { ManagementApiVersions, TunnelManagementHttpClient, TunnelClusterSource, TunnelClusterSelectionEventArgs, isClusterFallback } from '@microsoft/dev-tunnels-management';
import { Tunnel, TunnelPort, TunnelProgress, TunnelReportProgressEventArgs, ClusterRecommendationResponse, ClusterAvailability } from '@microsoft/dev-tunnels-contracts';
import { CancellationToken, CancellationTokenSource } from 'vscode-jsonrpc';

@suite
@slow(3000)
@timeout(10000)
export class TunnelManagementTests {

    private readonly managementClient: TunnelManagementHttpClient;

    private static readonly testServiceUri = 'http://global.tunnels.test.api.visualstudio.com';

    public constructor() {
        this.managementClient = new TunnelManagementHttpClient(
            'test/0.0.0', ManagementApiVersions.Version20230927preview, undefined, TunnelManagementTests.testServiceUri);
        (<any>this.managementClient).axiosRequest = this.mockAxiosRequest.bind(this);
    }

    private lastRequest?: {
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
    };
    private nextResponse?: any;

    private async mockAxiosRequest(config: AxiosRequestConfig, cancellation: CancellationToken): Promise<AxiosResponse> {
        this.lastRequest = { method: config.method as Method, uri: config.url || '', data: config.data, config };
        
        if (this.nextResponse instanceof AxiosError) {
            throw this.nextResponse;
        }

        var response = {
            data: this.nextResponse,
            status: 0,
            statusText: '',
            headers: {},
            config
        } as AxiosResponse;

        // simulate an Axios connection timeout
        var token = (cancellation as any);
        if (token?.forceConnection) {
            token.tokenSource?.cancel();
            throw new AxiosError('Network Error');
        }

        // simulate an Axios server response timeout
        if (token?.forceTimeout) {
            throw new AxiosError('', 'ECONNABORTED');
        }

        return Promise.resolve(response);
    }

    @test
    public async reportProgress() {
        let progressEvents: TunnelReportProgressEventArgs[] = [];
        this.managementClient.onReportProgress((e) => {
            progressEvents.push(e)
        });

        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
            accessTokens: {
                'manage': 'manage-token-1',
                'connect': 'connect-token-1',
            },
        };

        await this.managementClient.getTunnelPort(requestTunnel, 9900);

        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.CompletedGetTunnelPort);
        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.CompletedSendTunnelRequest);
        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.StartingSendTunnelRequest);
        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.StartingRequestConfig);
        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.StartingRequestUri);
        assert.strictEqual(progressEvents.pop()?.progress, TunnelProgress.StartingGetTunnelPort);
    }

    @test
    public async listTunnelsInCluster() {
        this.nextResponse = [];
        const testClusterId = 'test';
        await this.managementClient.listTunnels(testClusterId);
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://' + testClusterId + '.'));
        assert(!this.lastRequest.uri.includes('global=true'));
        assert(this.lastRequest.uri.includes('api-version=2023-09-27-preview'));
    }

    @test
    public async listTunnelsGlobal() {
        this.nextResponse = [];
        await this.managementClient.listTunnels();
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://global.'));
        assert(this.lastRequest.uri.includes('global=true'));
        assert(this.lastRequest.uri.includes('api-version=2023-09-27-preview'));
    }

    @test
    public async listTunnelsIncludePorts() {
        this.nextResponse = [];
        await this.managementClient.listTunnels(undefined, undefined, { includePorts: true });
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://global.'));
        assert(this.lastRequest.uri.includes('includePorts=true&global=true'));
        assert(this.lastRequest.uri.includes('api-version=2023-09-27-preview'));
    }

    @test
    public async listUserLimits() {
        this.nextResponse = [];
        await this.managementClient.listUserLimits();
        assert(this.lastRequest && this.lastRequest.uri);
        assert.equal(this.lastRequest.method, 'GET');
        assert(this.lastRequest.uri.includes('/userlimits'));
        assert(this.lastRequest.uri.includes('api-version=2023-09-27-preview'));
    }

    @test
    public async timeoutServerResponse() {
        this.nextResponse = [];

        let error: any | undefined = undefined;
        try {
            const cts = new CancellationTokenSource();
            // Add additional properties on token.
            // These will be used to simulate axios responses
            const token = (cts.token as any);
            token.forceTimeout = true;
            await this.managementClient.listUserLimits(cts.token);
        } catch (e) {
            error = e;
        }

        assert.match(error?.message, /ECONNABORTED: \(timeout\)/);
        assert.strictEqual(error?.code, 'ECONNABORTED');
    }

    @test
    public async timeoutServerConnection() {
        this.nextResponse = [];

        let error: any | undefined = undefined;
        try {
            const cts = new CancellationTokenSource();

            // Add additional properties on token.
            // These will be used to simulate axios responses
            const token = (cts.token as any);
            token.forceConnection = true;
            token.tokenSource = cts;
            await this.managementClient.listUserLimits(cts.token);
        } catch (e) {
            error = e;
        }

        assert.match(error?.message, /ECONNABORTED: \(signal aborted\)/);
        assert.strictEqual(error?.code, 'ECONNABORTED');
    }

    @test
    public async configDoesNotContainHttpsAgentAndAdapter() {
        this.nextResponse = [];
        await this.managementClient.listUserLimits();
        assert(this.lastRequest);
        assert(this.lastRequest.config.httpsAgent === undefined);
        assert(this.lastRequest.config.adapter === undefined);
    }

    @test
    public async configContainsHttpsAgentAndAdapter() {
        // Create a mock https agent
        const httpsAgent = new https.Agent({
            rejectUnauthorized: true,
            keepAlive: true,
        });

        // Create a mock axios adapter
        interface AxiosAdapter {
            (config: AxiosRequestConfig): AxiosPromise<any>;
        }

        class AxiosAdapter implements AxiosAdapter {
            constructor(private client: any, private auth: any) { }
        }

        const axiosAdapter = new AxiosAdapter(axios, { auth: { username: 'test', password: 'test' } });

        // Create a management client with a mock https agent and adapter
        const managementClient = new TunnelManagementHttpClient(
            'test/0.0.0', ManagementApiVersions.Version20230927preview, undefined, 'http://global.tunnels.test.api.visualstudio.com', httpsAgent, axiosAdapter);
        (<any>managementClient).axiosRequest = this.mockAxiosRequest.bind(this);

        this.nextResponse = [];
        await managementClient.listUserLimits();
        assert(this.lastRequest);

        // Assert that the https agent and adapter are the same as the ones we passed into the constructor
        assert(this.lastRequest.config.httpsAgent === httpsAgent);
        assert(this.lastRequest.config.httpsAgent !== new https.Agent({
            rejectUnauthorized: true,
            keepAlive: true,
        }));
        assert(this.lastRequest.config.adapter === axiosAdapter);
        assert(this.lastRequest.config.adapter !== new AxiosAdapter(axios, { auth: { username: 'test', password: 'test' } }))
    }

    @test
    public async preserveAccessTokens() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
            accessTokens: {
                'manage': 'manage-token-1',
                'connect': 'connect-token-1',
            },
        };
        this.nextResponse = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
            accessTokens: {
                'manage': 'manage-token-2',
                'host': 'host-token-2',
            },
        };
        const resultTunnel = await this.managementClient.getTunnel(requestTunnel);
        assert(this.lastRequest && this.lastRequest.uri);
        assert(resultTunnel);
        assert(resultTunnel.accessTokens);
        assert.strictEqual(resultTunnel.accessTokens['connect'], 'connect-token-1'); // preserved
        assert.strictEqual(resultTunnel.accessTokens['host'], 'host-token-2');       // added
        assert.strictEqual(resultTunnel.accessTokens['manage'], 'manage-token-2');   // updated
    }

    @test
    public async createTunnelRetriesOnGeneratedIdConflict() {
        const requestTunnel = <Tunnel>{
            clusterId: 'clusterId',
        };

        let callCount = 0;
        let firstTunnelId: string | undefined;
        let secondTunnelId: string | undefined;

        const conflictError = new AxiosError();
        conflictError.config = {
            url: TunnelManagementTests.testServiceUri,
            headers: new AxiosHeaders(),
        };
        conflictError.response = {
            status: 409,
            statusText: 'Conflict',
            headers: new AxiosHeaders(),
            data: undefined,
            config: conflictError.config,
        };

        const originalAxiosRequest = (<any>this.managementClient).axiosRequest;
        (<any>this.managementClient).axiosRequest = async (
            config: AxiosRequestConfig,
            cancellation: CancellationToken,
        ): Promise<AxiosResponse> => {
            this.lastRequest = {
                method: config.method as Method,
                uri: config.url || '',
                data: config.data,
                config,
            };
            callCount++;

            const sentTunnel = config.data as Tunnel;
            assert.strictEqual(config.headers?.['If-None-Match'], '*');
            if (callCount === 1) {
                firstTunnelId = sentTunnel?.tunnelId;
                throw conflictError;
            }

            secondTunnelId = sentTunnel?.tunnelId;

            return {
                data: <Tunnel>{
                    tunnelId: sentTunnel?.tunnelId,
                    clusterId: requestTunnel.clusterId,
                },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        try {
            const resultTunnel = await this.managementClient.createTunnel(requestTunnel);

            assert.strictEqual(callCount, 2);
            assert.ok(firstTunnelId);
            assert.ok(secondTunnelId);
            assert.notStrictEqual(firstTunnelId, secondTunnelId);
            assert.strictEqual(resultTunnel.tunnelId, secondTunnelId);
            assert.strictEqual(requestTunnel.tunnelId, secondTunnelId);
        } finally {
            (<any>this.managementClient).axiosRequest = originalAxiosRequest;
        }
    }

    @test
    public async createTunnelSendsIfNoneMatchHeader() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
        };
        this.nextResponse = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
        };
        const options = {
            additionalHeaders: {
                'X-Test': 'value',
            },
        };

        await this.managementClient.createTunnel(requestTunnel, options);

        assert(this.lastRequest);
        const headers = this.lastRequest!.config.headers as { [name: string]: string };
        assert.strictEqual(headers['If-None-Match'], '*');
        assert.strictEqual(headers['If-Not-Match'], undefined);
        assert.strictEqual(headers['X-Test'], 'value');
        assert.strictEqual(
            Object.prototype.hasOwnProperty.call(options.additionalHeaders, 'If-None-Match'),
            false,
        );
    }

    @test
    public async createTunnelPortSendsIfNoneMatchHeader() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
        };
        const requestPort = <TunnelPort>{
            portNumber: 9900,
        };
        this.nextResponse = <TunnelPort>{
            portNumber: 9900,
        };
        const options = {
            additionalHeaders: {
                'X-Test': 'value',
            },
        };

        await this.managementClient.createTunnelPort(requestTunnel, requestPort, options);

        assert(this.lastRequest);
        const headers = this.lastRequest!.config.headers as { [name: string]: string };
        assert.strictEqual(headers['If-None-Match'], '*');
        assert.strictEqual(headers['If-Not-Match'], undefined);
        assert.strictEqual(headers['X-Test'], 'value');
        assert.strictEqual(
            Object.prototype.hasOwnProperty.call(options.additionalHeaders, 'If-None-Match'),
            false,
        );
    }

    @test
    public async getClusterRecommendationsReturnsResponse() {
        const response = <ClusterRecommendationResponse>{
            preferredClusterId: 'usw2',
            recommendedClusterId: 'usw4',
            isFallback: true,
            recommendations: [
                {
                    clusterId: 'usw4',
                    azureLocation: 'WestUs2',
                    azureGeo: 'United States',
                    clusterUri: 'https://usw4.ci.tunnels.dev.api.visualstudio.com',
                    availability: ClusterAvailability.Available,
                    utilizationPercent: 12.5,
                    reason: 'Preferred cluster available',
                },
            ],
        };
        this.nextResponse = response;

        const result = await this.managementClient.getClusterRecommendations();

        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.includes('/clusters/recommendations'));
        assert(result);
        assert.strictEqual(result.recommendedClusterId, 'usw4');
        assert.strictEqual(result.recommendations.length, 1);
        assert.strictEqual(result.recommendations[0].availability, ClusterAvailability.Available);
        assert.strictEqual(result.recommendations[0].utilizationPercent, 12.5);
    }

    @test
    public async getClusterRecommendationsPassesQueryParameters() {
        this.nextResponse = <ClusterRecommendationResponse>{ recommendations: [] };

        await this.managementClient.getClusterRecommendations('usw2', 'us');

        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.includes('preferredClusterId=usw2'));
        assert(this.lastRequest.uri.includes('requiredGeo=us'));
    }

    @test
    public async createTunnelAutoRecommendsWhenClusterIdNotSet() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            // clusterId intentionally not set, so the client auto-recommends.
        };

        let recommendationCalls = 0;
        let createCalls = 0;
        let createUri = '';

        const originalAxiosRequest = (<any>this.managementClient).axiosRequest;
        (<any>this.managementClient).axiosRequest = async (
            config: AxiosRequestConfig,
            cancellation: CancellationToken,
        ): Promise<AxiosResponse> => {
            const uri = config.url || '';
            if (uri.includes('/recommendations')) {
                recommendationCalls++;
                return {
                    data: { recommendedClusterId: 'usw4', recommendations: [] },
                    status: 200,
                    statusText: 'OK',
                    headers: {},
                    config,
                } as AxiosResponse;
            }

            createCalls++;
            createUri = uri;
            const sentTunnel = config.data as Tunnel;
            return {
                data: <Tunnel>{ tunnelId: sentTunnel?.tunnelId, clusterId: 'usw4' },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        try {
            const result = await this.managementClient.createTunnel(requestTunnel);

            assert.strictEqual(recommendationCalls, 1);
            assert.strictEqual(createCalls, 1);
            assert.strictEqual(requestTunnel.clusterId, 'usw4');
            assert(createUri.includes('usw4.tunnels.test'));
            assert(result);
        } finally {
            (<any>this.managementClient).axiosRequest = originalAxiosRequest;
        }
    }

    @test
    public async createTunnelForwardsRequiredGeoFromOptionsToRecommendation() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            // clusterId intentionally not set, so the client auto-recommends.
        };

        let recommendationUri = '';
        let createUri = '';

        const originalAxiosRequest = (<any>this.managementClient).axiosRequest;
        (<any>this.managementClient).axiosRequest = async (
            config: AxiosRequestConfig,
            cancellation: CancellationToken,
        ): Promise<AxiosResponse> => {
            const uri = config.url || '';
            if (uri.includes('/recommendations')) {
                recommendationUri = uri;
                return {
                    data: { recommendedClusterId: 'usw4', recommendations: [] },
                    status: 200,
                    statusText: 'OK',
                    headers: {},
                    config,
                } as AxiosResponse;
            }

            createUri = uri;
            const sentTunnel = config.data as Tunnel;
            return {
                data: <Tunnel>{ tunnelId: sentTunnel?.tunnelId, clusterId: 'usw4' },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        try {
            await this.managementClient.createTunnel(requestTunnel, { requiredGeo: 'us' });

            // requiredGeo flows to the recommendations request...
            assert(recommendationUri.includes('requiredGeo=us'));

            // ...but is NOT included on the create-tunnel request itself.
            assert(!createUri.includes('requiredGeo'));
        } finally {
            (<any>this.managementClient).axiosRequest = originalAxiosRequest;
        }
    }

    @test
    public async createTunnelSkipsRecommendWhenClusterIdSet() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'usw2',
        };

        let recommendationCalls = 0;
        let createCalls = 0;
        let createUri = '';

        const originalAxiosRequest = (<any>this.managementClient).axiosRequest;
        (<any>this.managementClient).axiosRequest = async (
            config: AxiosRequestConfig,
            cancellation: CancellationToken,
        ): Promise<AxiosResponse> => {
            const uri = config.url || '';
            if (uri.includes('/recommendations')) {
                recommendationCalls++;
            } else {
                createCalls++;
                createUri = uri;
            }
            const sentTunnel = config.data as Tunnel;
            return {
                data: <Tunnel>{ tunnelId: sentTunnel?.tunnelId, clusterId: 'usw2' },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        try {
            await this.managementClient.createTunnel(requestTunnel);

            assert.strictEqual(recommendationCalls, 0);
            assert.strictEqual(createCalls, 1);
            assert(createUri.includes('usw2.tunnels.test'));
        } finally {
            (<any>this.managementClient).axiosRequest = originalAxiosRequest;
        }
    }

    @test
    public async createTunnelFallsBackOnRecommendFailure() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            // clusterId not set; recommendations call will fail.
        };

        let createCalls = 0;
        let createUri = '';

        const recommendError = new AxiosError();
        recommendError.config = {
            url: TunnelManagementTests.testServiceUri,
            headers: new AxiosHeaders(),
        };
        recommendError.response = {
            status: 500,
            statusText: 'Internal Server Error',
            headers: new AxiosHeaders(),
            data: undefined,
            config: recommendError.config,
        };

        const originalAxiosRequest = (<any>this.managementClient).axiosRequest;
        (<any>this.managementClient).axiosRequest = async (
            config: AxiosRequestConfig,
            cancellation: CancellationToken,
        ): Promise<AxiosResponse> => {
            const uri = config.url || '';
            if (uri.includes('/recommendations')) {
                throw recommendError;
            }

            createCalls++;
            createUri = uri;
            const sentTunnel = config.data as Tunnel;
            return {
                data: <Tunnel>{ tunnelId: sentTunnel?.tunnelId },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        try {
            const result = await this.managementClient.createTunnel(requestTunnel);

            assert.strictEqual(createCalls, 1);
            assert.strictEqual(requestTunnel.clusterId, undefined);

            // No cluster prefix was added: routing falls back to the global hostname.
            assert(createUri.includes('global.tunnels.test'));
            assert(result);
        } finally {
            (<any>this.managementClient).axiosRequest = originalAxiosRequest;
        }
    }

    @test
    public async handleFirewallResponse() {
        const requestTunnel = <Tunnel>{
            tunnelId: 'tunnelid',
            clusterId: 'clusterId',
        };
        const firewallError = new AxiosError();
        firewallError.config = {
            url: TunnelManagementTests.testServiceUri,
            headers: new AxiosHeaders(),
        };
        firewallError.response = {
            status: 403,
            statusText: 'Forbidden',
            headers: new AxiosHeaders(),
            data: undefined,
            config: firewallError.config,
        };
        this.nextResponse = firewallError;

        let error: any | undefined = undefined;
        try {
            await this.managementClient.getTunnel(requestTunnel);
        } catch (e) {
            error = e;
        }

        assert(error);
        assert.match(error.message, /firewall/);
        assert.match(error.message, new RegExp(new URL(TunnelManagementTests.testServiceUri).host));
    }

    @test
    public async customDomainDoesNotModifyHostname() {
        const client = TunnelManagementHttpClient.forCustomDomain(
            'app.github.dev',
            'test/0.0.0',
            ManagementApiVersions.Version20230927preview,
        );

        let capturedUri: string | undefined;
        (<any>client).axiosRequest = async (config: AxiosRequestConfig) => {
            capturedUri = config.url;
            return {
                data: { tunnelId: 'tnnl0001', clusterId: 'usw2' },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        const tunnel: Tunnel = { tunnelId: 'tnnl0001', clusterId: 'usw2' };
        await client.getTunnel(tunnel);

        assert.ok(capturedUri);
        const url = new URL(capturedUri!);
        assert.strictEqual(url.hostname, 'cp.app.github.dev');
    }

    @test
    public async standardServiceUriReplacesClusterIdInHostname() {
        const client = new TunnelManagementHttpClient(
            'test/0.0.0',
            ManagementApiVersions.Version20230927preview,
            undefined,
            TunnelManagementTests.testServiceUri,
        );

        let capturedUri: string | undefined;
        (<any>client).axiosRequest = async (config: AxiosRequestConfig) => {
            capturedUri = config.url;
            return {
                data: { tunnelId: 'tnnl0001', clusterId: 'usw2' },
                status: 200,
                statusText: 'OK',
                headers: {},
                config,
            } as AxiosResponse;
        };

        const tunnel: Tunnel = { tunnelId: 'tnnl0001', clusterId: 'usw2' };
        await client.getTunnel(tunnel);

        assert.ok(capturedUri);
        const url = new URL(capturedUri!);
        assert.ok(url.hostname.startsWith('usw2.'), `Expected hostname to start with usw2., got ${url.hostname}`);
    }

    private static readonly clusterSourceHeaderName = 'X-Tunnel-Cluster-Source';

    /**
     * Creates a client whose transport is driven by the given handler and records every
     * request it makes, so tests can assert on the auth and cluster-source headers.
     */
    private createRecordingClient(
        token: string | undefined,
        handler: (config: AxiosRequestConfig) => AxiosResponse,
    ): { client: TunnelManagementHttpClient; requests: AxiosRequestConfig[] } {
        const requests: AxiosRequestConfig[] = [];
        const client = new TunnelManagementHttpClient(
            'test/0.0.0',
            ManagementApiVersions.Version20230927preview,
            token ? async () => token : undefined,
            TunnelManagementTests.testServiceUri,
        );
        (<any>client).axiosRequest = async (config: AxiosRequestConfig) => {
            requests.push(config);
            return handler(config);
        };
        return { client, requests };
    }

    private static isRecommendationsRequest(config: AxiosRequestConfig): boolean {
        return (config.url ?? '').includes('/clusters/recommendations');
    }

    private static okResponse(config: AxiosRequestConfig, data: any): AxiosResponse {
        return { data, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse;
    }

    private static failure(config: AxiosRequestConfig, status: number): AxiosError {
        const error = new AxiosError('request failed');
        error.response = {
            data: {},
            status,
            statusText: '',
            headers: {} as AxiosHeaders,
            config,
        } as AxiosResponse;
        return error;
    }

    private static authorizationOf(config: AxiosRequestConfig): string | undefined {
        return (config.headers as { [name: string]: string })?.['Authorization'];
    }

    private static clusterSourceOf(config: AxiosRequestConfig): string | undefined {
        return (config.headers as { [name: string]: string })?.[
            TunnelManagementTests.clusterSourceHeaderName
        ];
    }

    // The recommendations call must send the caller's token so the service can identify
    // them and apply their service tier.
    @test
    public async getClusterRecommendationsSendsAuthorizationHeader() {
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) =>
            TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{
                recommendedClusterId: 'euw',
            }),
        );

        const result = await client.getClusterRecommendations();

        assert.strictEqual(result.recommendedClusterId, 'euw');
        assert.strictEqual(requests.length, 1);
        assert.strictEqual(TunnelManagementTests.authorizationOf(requests[0]), 'Bearer test-token');
    }

    // With no token configured the call must still work anonymously, and must not send an
    // empty Authorization header, which the service would reject.
    @test
    public async getClusterRecommendationsWithoutTokenSendsNoAuthorizationHeader() {
        const { client, requests } = this.createRecordingClient(undefined, (config) =>
            TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{
                recommendedClusterId: 'euw',
            }),
        );

        await client.getClusterRecommendations();

        assert.strictEqual(requests.length, 1);
        assert.strictEqual(TunnelManagementTests.authorizationOf(requests[0]), undefined);
    }

    // The service rejects a bad token before the controller runs, so it never falls back to
    // treating the caller as anonymous. Without the retry, one expired token would silently
    // disable recommendation-based routing for that caller.
    @test
    public async getClusterRecommendationsRetriesAnonymouslyAfterUnauthorized() {
        const { client, requests } = this.createRecordingClient('Bearer expired-token', (config) => {
            if (TunnelManagementTests.authorizationOf(config)) {
                throw TunnelManagementTests.failure(config, 401);
            }
            return TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{
                recommendedClusterId: 'euw',
            });
        });

        const result = await client.getClusterRecommendations();

        assert.strictEqual(result.recommendedClusterId, 'euw');
        assert.strictEqual(requests.length, 2);
        assert.strictEqual(TunnelManagementTests.authorizationOf(requests[1]), undefined);
    }

    // A non-auth failure must not trigger the anonymous retry; retrying would not help and
    // would double the latency of every failure.
    @test
    public async getClusterRecommendationsDoesNotRetryOnServerError() {
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            throw TunnelManagementTests.failure(config, 500);
        });

        await assert.rejects(async () => await client.getClusterRecommendations());
        assert.strictEqual(requests.length, 1);
    }

    // When the caller specifies a cluster there is nothing to recommend, so no call is made
    // and the create reports that the cluster was chosen explicitly.
    @test
    public async createTunnelWithExplicitClusterReportsExplicitSource() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            assert.ok(
                !TunnelManagementTests.isRecommendationsRequest(config),
                'recommendations should not be requested when a cluster is specified',
            );
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        await client.createTunnel(<Tunnel>{ clusterId: 'euw' });

        assert.strictEqual(selections.length, 0);
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'explicit',
        );
    }

    // The happy path: the recommendation is used and reported as such.
    @test
    public async createTunnelReportsRecommendedCluster() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            if (TunnelManagementTests.isRecommendationsRequest(config)) {
                return TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{
                    recommendedClusterId: 'euw',
                });
            }
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        await client.createTunnel(<Tunnel>{});

        assert.strictEqual(selections.length, 1);
        assert.strictEqual(selections[0].source, TunnelClusterSource.recommended);
        assert.strictEqual(selections[0].clusterId, 'euw');
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'recommended',
        );
    }

    // Routing is correct here but the caller was not identified, so they cannot be assigned
    // a service tier. That is a token problem the caller would otherwise never learn about.
    @test
    public async createTunnelReportsRecommendationAfterAuthRejected() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer expired-token', (config) => {
            if (TunnelManagementTests.isRecommendationsRequest(config)) {
                if (TunnelManagementTests.authorizationOf(config)) {
                    throw TunnelManagementTests.failure(config, 401);
                }
                return TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{
                    recommendedClusterId: 'euw',
                });
            }
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        await client.createTunnel(<Tunnel>{});

        assert.strictEqual(selections.length, 1);
        assert.strictEqual(
            selections[0].source,
            TunnelClusterSource.recommendedAfterAuthRejected,
        );
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'recommended-after-auth-rejected',
        );
    }

    // The create still succeeds via global routing, so the fallback is invisible without
    // the event and the header.
    @test
    public async createTunnelReportsFallbackOnRecommendationError() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            if (TunnelManagementTests.isRecommendationsRequest(config)) {
                throw TunnelManagementTests.failure(config, 500);
            }
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        const tunnel = await client.createTunnel(<Tunnel>{});

        assert.ok(tunnel, 'create should still succeed via global routing');
        assert.strictEqual(selections.length, 1);
        assert.strictEqual(selections[0].source, TunnelClusterSource.fallbackError);
        assert.ok(selections[0].error, 'expected the underlying error to be reported');
        assert.ok(isClusterFallback(selections[0].source));
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'fallback-error',
        );
    }

    // An auth failure that survives the anonymous retry is distinguished from a generic
    // failure, because it points at the service rejecting the request rather than being
    // down.
    @test
    public async createTunnelReportsFallbackWhenAuthFailsEvenAnonymously() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            if (TunnelManagementTests.isRecommendationsRequest(config)) {
                throw TunnelManagementTests.failure(config, 401);
            }
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        await client.createTunnel(<Tunnel>{});

        assert.strictEqual(selections.length, 1);
        assert.strictEqual(selections[0].source, TunnelClusterSource.fallbackAuthFailed);
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'fallback-auth-failed',
        );
    }

    // A successful call that recommends nothing is a distinct condition from a failure, and
    // means the service had no cluster to offer.
    @test
    public async createTunnelReportsFallbackOnEmptyRecommendation() {
        const selections: TunnelClusterSelectionEventArgs[] = [];
        const { client, requests } = this.createRecordingClient('Bearer test-token', (config) => {
            if (TunnelManagementTests.isRecommendationsRequest(config)) {
                return TunnelManagementTests.okResponse(config, <ClusterRecommendationResponse>{});
            }
            return TunnelManagementTests.okResponse(config, <Tunnel>{ tunnelId: 'tnnl0001' });
        });
        client.onClusterSelected((e) => selections.push(e));

        await client.createTunnel(<Tunnel>{});

        assert.strictEqual(selections.length, 1);
        assert.strictEqual(selections[0].source, TunnelClusterSource.fallbackEmpty);
        assert.strictEqual(
            TunnelManagementTests.clusterSourceOf(requests[requests.length - 1]),
            'fallback-empty',
        );
    }
}
