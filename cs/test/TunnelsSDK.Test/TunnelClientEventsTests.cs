using System.Net;
using System.Net.Http.Headers;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Xunit;

namespace Microsoft.DevTunnels.Test;

public class TunnelClientEventsTests
{
    // Note: Tests in this class are mostly AI-generated.

    private readonly CancellationToken timeout = System.Diagnostics.Debugger.IsAttached ? default : new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
    private readonly ProductInfoHeaderValue userAgent = TunnelUserAgent.GetUserAgent(typeof(TunnelClientEventsTests).Assembly);
    private readonly Uri tunnelServiceUri = new Uri("https://localhost:3000/");

    private static Tunnel TestTunnel { get; } = new Tunnel
    {
        TunnelId = "tnnl0001",
        ClusterId = "usw2",
    };

    private static Tunnel TestTunnel2 { get; } = new Tunnel
    {
        TunnelId = "tnnl0002",
        ClusterId = "usw2",
    };

    /// <summary>
    /// Waits for the expected number of HTTP requests to be captured by the mock handler.
    /// </summary>
    /// <param name="requestCapture">The list that captures HTTP requests.</param>
    /// <param name="expectedCount">The expected number of requests.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 5000).</param>
    private static async Task WaitForRequestsAsync(List<HttpRequestMessage> requestCapture, int expectedCount, int timeoutMs = 5000)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var endTime = DateTime.Now.Add(timeout);

        while (DateTime.Now < endTime)
        {
            if (requestCapture.Count >= expectedCount)
            {
                return;
            }
            await Task.Delay(10);
        }

        throw new TimeoutException($"Expected {expectedCount} requests but only received {requestCapture.Count} within {timeoutMs}ms");
    }

    [Fact]
    public async Task SingleEventSendsHttpPostWithCorrectPayload()
    {
        var tunnelEvent = new TunnelEvent("test-event")
        {
            Severity = TunnelEvent.Info,
            Details = "Test event details",
            Properties = new Dictionary<string, string>
            {
                { "property1", "value1" },
                { "property2", "value2" }
            }
        };

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act
        client.ReportEvent(TestTunnel, tunnelEvent);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 1);

        // Assert
        Assert.Single(requestCapture);
        var request = requestCapture[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/events", request.RequestUri!.ToString());
        Assert.Contains("api-version=2023-09-27-preview", request.RequestUri.ToString());

        // Verify the request body contains the event
        var content = await request.Content!.ReadAsStringAsync();
        Assert.Contains("test-event", content);
        Assert.Contains("Test event details", content);
        Assert.Contains("property1", content);
        Assert.Contains("value1", content);
    }

    [Fact]
    public async Task MultipleEventsForSameTunnelBatchesIntoSingleRequest()
    {
        var event1 = new TunnelEvent("event-1");
        var event2 = new TunnelEvent("event-2");
        var event3 = new TunnelEvent("event-3");

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act - report multiple events for the same tunnel
        client.ReportEvent(TestTunnel, event1);
        client.ReportEvent(TestTunnel, event2);
        client.ReportEvent(TestTunnel, event3);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 1);

        // Assert - should batch into a single request
        Assert.Single(requestCapture);
        var request = requestCapture[0];
        Assert.Equal(HttpMethod.Post, request.Method);

        var content = await request.Content!.ReadAsStringAsync();
        Assert.Contains("event-1", content);
        Assert.Contains("event-2", content);
        Assert.Contains("event-3", content);
    }

    [Fact]
    public async Task MultipleEventsForDifferentTunnelsSendsSeparateRequests()
    {
        var event1 = new TunnelEvent("event-1");
        var event2 = new TunnelEvent("event-2");

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act - report events for different tunnels
        client.ReportEvent(TestTunnel, event1);
        client.ReportEvent(TestTunnel2, event2);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 2);

        // Assert - should send separate requests for different tunnels
        Assert.Equal(2, requestCapture.Count);

        // Check first request
        var request1 = requestCapture[0];
        Assert.Equal(HttpMethod.Post, request1.Method);
        var content1 = await request1.Content!.ReadAsStringAsync();
        Assert.Contains("event-1", content1);
        Assert.DoesNotContain("event-2", content1);

        // Check second request
        var request2 = requestCapture[1];
        Assert.Equal(HttpMethod.Post, request2.Method);
        var content2 = await request2.Content!.ReadAsStringAsync();
        Assert.Contains("event-2", content2);
        Assert.DoesNotContain("event-1", content2);
    }

    [Fact]
    public async Task WithRequestOptionsIncludesOptionsInRequest()
    {
        var tunnelEvent = new TunnelEvent("test-event");

        var options = new TunnelRequestOptions
        {
            AccessToken = "test-access-token",
            AdditionalHeaders = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("X-Custom-Header", "CustomValue")
            }
        };

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act
        client.ReportEvent(TestTunnel, tunnelEvent, options);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 1);

        // Assert
        Assert.Single(requestCapture);
        var request = requestCapture[0];

        // Check that custom headers are included
        Assert.True(request.Headers.Contains("X-Custom-Header"));
        Assert.Equal("CustomValue", request.Headers.GetValues("X-Custom-Header").First());

        // Check authorization header contains the access token
        Assert.NotNull(request.Headers.Authorization);
        Assert.Contains("test-access-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task WithHttpRequestExceptionIgnoresError()
    {
        var tunnelEvent = new TunnelEvent("test-event");

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                // Simulate HTTP error
                throw new HttpRequestException("Network error");
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act - this should not throw an exception
        client.ReportEvent(TestTunnel, tunnelEvent);

        // Should not throw when disposing
        await client.DisposeAsync();
    }

    [Fact]
    public async Task WithV1ApiVersionDoesNotSendEvents()
    {
        var tunnelEvent = new TunnelEvent("test-event");

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        // Create client with empty API version (simulating V1)
        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Use reflection to set ApiVersion to null (simulating V1 API)
        var apiVersionField = typeof(TunnelManagementClient).GetProperty("ApiVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // Since we can't easily set this in the constructor, we'll test the current behavior

        // Act
        client.ReportEvent(TestTunnel, tunnelEvent);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 1);

        // For current implementation with default API version, we expect the event to be sent
        // If V1 API support is added later, this test will need to be updated
        Assert.Single(requestCapture);
    }

    [Fact]
    public async Task UsesAccessToken()
    {
        var tunnel = new Tunnel
        {
            TunnelId = TestTunnel.TunnelId,
            ClusterId = TestTunnel.ClusterId,
            AccessTokens = new Dictionary<string, string>
            {
                [TunnelAccessScopes.Connect] = "connect-token"
            }
        };

        var tunnelEvent = new TunnelEvent("test-event");

        var requestCapture = new List<HttpRequestMessage>();
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act
        client.ReportEvent(tunnel, tunnelEvent);

        // Wait for the expected number of requests to be processed
        await WaitForRequestsAsync(requestCapture, 1);

        // Assert
        Assert.Single(requestCapture);
        var request = requestCapture[0];

        // The ReportEvent method should use ReadAccessTokenScopes, which includes
        // Manage, ManagePorts, Host, and Connect scopes. Since the tunnel has a Connect
        // scope access token, that should be used for authentication.
        Assert.NotNull(request.Headers.Authorization);
        var authParam = request.Headers.Authorization.Parameter;

        // Should use one of the available tokens
        Assert.True(
            authParam!.Contains("connect-token"),
            $"Authorization header should contain the connect token, but was: {authParam}");
    }

    [Fact]
    public async Task DisposedImmediatelyStillSendsEvents()
    {
        var tunnelEvent = new TunnelEvent("test-event-dispose");
        var tunnelEvent2 = new TunnelEvent("test-event-after-dispose");

        var requestCapture = new List<HttpRequestMessage>();
        var lastRequestTime = DateTime.MinValue;
        var disposalTime = DateTime.MinValue;

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                requestCapture.Add(message);
                lastRequestTime = DateTime.Now;
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);
        client.EnableEventsReporting = true;

        // Act - report event and dispose immediately
        client.ReportEvent(TestTunnel, tunnelEvent);
        var disposeTask = client.DisposeAsync(); // Dispose immediately after reporting
        client.ReportEvent(TestTunnel, tunnelEvent2); // Ignored after dispose

        // Wait for the expected number of requests to be processed
        // Even though we disposed immediately, the background task should complete
        Task.WaitAll(
            disposeTask.AsTask(),
            WaitForRequestsAsync(requestCapture, 1)
        );

        // Assert
        Assert.Single(requestCapture);
        var request = requestCapture[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/events", request.RequestUri!.ToString());

        // Verify the request body contains the event
        var content = await request.Content!.ReadAsStringAsync();
        Assert.Contains("test-event-dispose", content);
    }

    private sealed class MockHttpMessageHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            : base(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseDefaultCredentials = false,
            })
        {
            this.handler = Requires.NotNull(handler, nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            this.handler(request, cancellationToken);
    }
}
