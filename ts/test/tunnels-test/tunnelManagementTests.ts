// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import axios, { Axios, AxiosHeaders, AxiosError, AxiosPromise, AxiosRequestConfig, AxiosResponse, Method } from 'axios';
import * as https from 'https';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { ManagementApiVersions, TunnelManagementHttpClient } from '@microsoft/dev-tunnels-management';
import { Tunnel, TunnelProgress, TunnelReportProgressEventArgs } from '@microsoft/dev-tunnels-contracts';
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
}
