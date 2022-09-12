// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { TunnelManagementHttpClient } from '@vs/tunnels-management';
import { AxiosRequestConfig, Method } from 'axios';

@suite
@slow(3000)
@timeout(10000)
export class TunnelManagementTests {

    private readonly managementClient : TunnelManagementHttpClient;

    public constructor() {
        this.managementClient = new TunnelManagementHttpClient(
            'test/0.0.0', undefined, 'http://global.tunnels.test.api.visualstudio.com');
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
}
