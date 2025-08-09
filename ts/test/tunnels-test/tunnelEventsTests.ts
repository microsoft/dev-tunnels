// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as assert from 'assert';
import { AxiosHeaders, AxiosError, AxiosRequestConfig, AxiosResponse, Method } from 'axios';
import { suite, test, slow, timeout } from '@testdeck/mocha';
import { ManagementApiVersions, TunnelManagementHttpClient } from '@microsoft/dev-tunnels-management';
import { Tunnel, TunnelEvent } from '@microsoft/dev-tunnels-contracts';
import { CancellationToken } from 'vscode-jsonrpc';

@suite
@slow(3000)
@timeout(10000)
export class TunnelEventsTests {

    private readonly managementClient: TunnelManagementHttpClient;

    private static readonly testServiceUri = 'http://global.tunnels.test.api.visualstudio.com';

    public constructor() {
        this.managementClient = new TunnelManagementHttpClient(
            'test/0.0.0', ManagementApiVersions.Version20230927preview, undefined, TunnelEventsTests.testServiceUri);
        (<any>this.managementClient).axiosRequest = this.mockAxiosRequest.bind(this);
    }

    private requestCapture: Array<{
        method: Method,
        uri: string,
        data: any,
        config: AxiosRequestConfig,
    }> = [];
    private nextResponse?: any;

    /**
     * Waits for the expected number of HTTP requests to be captured by the mock handler.
     * @param expectedCount The expected number of requests.
     * @param timeoutMs Timeout in milliseconds (default: 5000).
     */
    private async waitForRequestsAsync(expectedCount: number, timeoutMs: number = 5000): Promise<void> {
        const startTime = Date.now();
        const endTime = startTime + timeoutMs;

        while (Date.now() < endTime) {
            if (this.requestCapture.length >= expectedCount) {
                return;
            }
            await new Promise(resolve => setTimeout(resolve, 10));
        }

        throw new Error(`Expected ${expectedCount} requests but only received ${this.requestCapture.length} within ${timeoutMs}ms`);
    }

    private async mockAxiosRequest(config: AxiosRequestConfig, cancellation: CancellationToken): Promise<AxiosResponse> {
        const request = { method: config.method as Method, uri: config.url || '', data: config.data, config };
        this.requestCapture.push(request);
        
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
    public async reportEventWithEventsDisabled() {
        // Test that no request is made when events reporting is disabled
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent: TunnelEvent = {
            name: 'test-event',
            severity: 'info',
            details: 'Test event details',
            properties: { 'test-prop': 'test-value' },
        };

        // Events reporting is disabled by default
        this.managementClient.enableEventsReporting = false;
        this.requestCapture = [];

        // Report an event
        this.managementClient.reportEvent(testTunnel, testEvent);

        // Wait a bit to see if any request is made
        await new Promise(resolve => setTimeout(resolve, 200));

        // No request should have been made
        assert.strictEqual(this.requestCapture.length, 0);
    }

    @test
    public async reportEventWithEventsEnabled() {
        // Test that events are queued and uploaded when events reporting is enabled
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent: TunnelEvent = {
            name: 'test-event',
            severity: 'info',
            details: 'Test event details',
            properties: { 'test-prop': 'test-value' },
        };

        // Enable events reporting
        this.managementClient.enableEventsReporting = true;
        this.nextResponse = true; // Mock success response
        this.requestCapture = [];

        // Report an event
        this.managementClient.reportEvent(testTunnel, testEvent);

        // Wait for the expected number of requests to be processed
        await this.waitForRequestsAsync(1);

        // A request should have been made
        assert.strictEqual(this.requestCapture.length, 1);
        const request = this.requestCapture[0];
        assert.strictEqual(request.method, 'POST');
        assert(request.uri.includes('/events'));
        assert(request.uri.includes('api-version=2023-09-27-preview'));
        assert(request.uri.includes(testTunnel.clusterId!));
        assert(request.uri.includes(testTunnel.tunnelId!));

        // Check the request body contains the event
        assert(Array.isArray(request.data));
        assert.strictEqual(request.data.length, 1);
        const sentEvent = request.data[0];
        assert.strictEqual(sentEvent.name, testEvent.name);
        assert.strictEqual(sentEvent.severity, testEvent.severity);
        assert.strictEqual(sentEvent.details, testEvent.details);
        assert.deepStrictEqual(sentEvent.properties, testEvent.properties);
    }

    @test
    public async reportMultipleEvents() {
        // Test that multiple events for the same tunnel are batched together
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent1: TunnelEvent = {
            name: 'test-event-1',
            severity: 'info',
            details: 'First test event',
            properties: { 'event': '1' },
        };

        const testEvent2: TunnelEvent = {
            name: 'test-event-2',
            severity: 'warning',
            details: 'Second test event',
            properties: { 'event': '2' },
        };

        // Enable events reporting
        this.managementClient.enableEventsReporting = true;
        this.nextResponse = true; // Mock success response
        this.requestCapture = [];

        // Report multiple events quickly
        this.managementClient.reportEvent(testTunnel, testEvent1);
        this.managementClient.reportEvent(testTunnel, testEvent2);

        // Wait for the expected number of requests to be processed
        await this.waitForRequestsAsync(1);

        // A single request should have been made with both events
        assert.strictEqual(this.requestCapture.length, 1);
        const request = this.requestCapture[0];
        assert.strictEqual(request.method, 'POST');
        assert(request.uri.includes('/events'));

        // Check the request body contains both events
        assert(Array.isArray(request.data));
        assert.strictEqual(request.data.length, 2);
        
        const sentEvents = request.data;
        assert.strictEqual(sentEvents[0].name, testEvent1.name);
        assert.strictEqual(sentEvents[1].name, testEvent2.name);
    }

    @test
    public async reportEventErrorIsIgnored() {
        // Test that errors during event upload are ignored and don't throw
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent: TunnelEvent = {
            name: 'test-event',
            severity: 'error',
            details: 'Test error event',
        };

        // Enable events reporting
        this.managementClient.enableEventsReporting = true;
        
        // Mock an error response
        this.nextResponse = new AxiosError('Network error', '500', undefined, undefined, {
            status: 500,
            statusText: 'Internal Server Error',
            headers: new AxiosHeaders(),
            data: undefined,
            config: { headers: new AxiosHeaders() } as any,
        });
        this.requestCapture = [];

        // Report an event - this should not throw even though the upload fails
        this.managementClient.reportEvent(testTunnel, testEvent);

        // Wait for the expected number of requests to be processed
        await this.waitForRequestsAsync(1);

        // A request should have been attempted
        assert.strictEqual(this.requestCapture.length, 1);
        const request = this.requestCapture[0];
        assert.strictEqual(request.method, 'POST');
    }

    @test
    public async reportEventValidatesParameters() {
        // Test parameter validation
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent: TunnelEvent = {
            name: 'test-event',
        };

        this.managementClient.enableEventsReporting = true;

        // Test null tunnel parameter
        assert.throws(() => {
            this.managementClient.reportEvent(null as any, testEvent);
        }, /A tunnel is required/);

        // Test null event parameter
        assert.throws(() => {
            this.managementClient.reportEvent(testTunnel, null as any);
        }, /A tunnelEvent is required/);
    }

    @test
    public async disposedImmediatelyStillSendsEvents() {
        // Test that events reported before disposal are still sent even if client is disposed immediately
        const testTunnel: Tunnel = {
            tunnelId: 'test-tunnel-id',
            clusterId: 'test-cluster-id',
        };

        const testEvent: TunnelEvent = {
            name: 'test-event-dispose',
            severity: 'info',
            details: 'Test event before dispose',
        };

        const testEvent2: TunnelEvent = {
            name: 'test-event-after-dispose',
            severity: 'info',
            details: 'Test event after dispose',
        };

        // Enable events reporting
        this.managementClient.enableEventsReporting = true;
        this.nextResponse = true; // Mock success response
        this.requestCapture = [];

        // Report an event and dispose immediately
        this.managementClient.reportEvent(testTunnel, testEvent);
        const disposePromise = this.managementClient.dispose(); // Dispose immediately after reporting
        this.managementClient.reportEvent(testTunnel, testEvent2); // This should be ignored after dispose

        // Wait for both disposal and request processing to complete
        await Promise.all([
            disposePromise,
            this.waitForRequestsAsync(1)
        ]);

        // Even though we disposed immediately, the background task should complete
        // and send the event that was reported before disposal
        assert.strictEqual(this.requestCapture.length, 1);
        const request = this.requestCapture[0];
        assert.strictEqual(request.method, 'POST');
        assert(request.uri.includes('/events'));

        // Verify the request body contains only the first event (before disposal)
        assert(Array.isArray(request.data));
        assert.strictEqual(request.data.length, 1);
        const sentEvent = request.data[0];
        assert.strictEqual(sentEvent.name, testEvent.name);
        assert.strictEqual(sentEvent.details, testEvent.details);
        
        // The second event should not be present since it was reported after disposal
        assert(!request.data.some((e: any) => e.name === testEvent2.name));
    }
}
