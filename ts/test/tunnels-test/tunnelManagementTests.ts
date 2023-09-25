// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import axios, { AxiosPromise, AxiosRequestConfig, Method } from 'axios';
import * as https from 'https';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { TunnelManagementHttpClient } from '@microsoft/dev-tunnels-management';
import { Tunnel } from '@microsoft/dev-tunnels-contracts';

@suite
@slow(3000)
@timeout(10000)
export class TunnelManagementTests {

    private readonly managementClient: TunnelManagementHttpClient;

    public constructor() {
        this.managementClient = new TunnelManagementHttpClient(
            'test/0.0.0', "2023-09-27-preview", undefined, 'http://global.tunnels.test.api.visualstudio.com');
        (<any>this.managementClient).request = this.mockRequest.bind(this);
    }

    private lastRequest?: {
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
    };
    private nextResponse?: any;

    private async mockRequest<TResponse>(
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
    ): Promise<TResponse> {
        this.lastRequest = { method, uri, data, config };
        return Promise.resolve(this.nextResponse as TResponse);
    }

    @test
    public async listTunnelsInCluster() {
        this.nextResponse = [];
        const testClusterId = 'test';
        await this.managementClient.listTunnels(testClusterId);
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://' + testClusterId + '.'));
        assert(!this.lastRequest.uri.includes('global=true'));
    }

    @test
    public async listTunnelsGlobal() {
        this.nextResponse = [];
        await this.managementClient.listTunnels();
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://global.'));
        assert(this.lastRequest.uri.includes('global=true'));
    }

    @test
    public async listTunnelsIncludePorts() {
        this.nextResponse = [];
        await this.managementClient.listTunnels(undefined, undefined, { includePorts: true });
        assert(this.lastRequest && this.lastRequest.uri);
        assert(this.lastRequest.uri.startsWith('http://global.'));
        assert(this.lastRequest.uri.includes('includePorts=true&global=true'));
    }

    @test
    public async listUserLimits() {
        this.nextResponse = [];
        await this.managementClient.listUserLimits();
        assert(this.lastRequest && this.lastRequest.uri);
        assert.equal(this.lastRequest.method, 'GET');
        assert(this.lastRequest.uri.endsWith('/api/v1/userlimits'));
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
            'test/0.0.0',"2023-09-27-preview", undefined, 'http://global.tunnels.test.api.visualstudio.com', httpsAgent, axiosAdapter);
        (<any>managementClient).request = this.mockRequest.bind(this);

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
}
