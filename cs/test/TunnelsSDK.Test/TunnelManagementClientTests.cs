using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Xunit;

namespace Microsoft.DevTunnels.Test;
public class TunnelManagementClientTests
{
    private const string TunnelId = "tnnl0001";
    private const string ClusterId = "usw2";

    private readonly CancellationToken timeout = System.Diagnostics.Debugger.IsAttached ? default : new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
    private readonly ProductInfoHeaderValue userAgent = TunnelUserAgent.GetUserAgent(typeof(TunnelManagementClientTests).Assembly);
    private readonly Uri tunnelServiceUri = new Uri("https://localhost:3000/");

    [Fact]
    public async Task HttpRequestOptions()
    {
        var options = new TunnelRequestOptions()
        {
            HttpRequestOptions = new Dictionary<string, object>
            {
                { "foo", "bar" },
                { "bazz", 100 },
            }
        };

        var tunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                Assert.True(message.Options.TryGetValue(new HttpRequestOptionsKey<string>("foo"), out string strValue) && strValue == "bar");
                Assert.True(message.Options.TryGetValue(new HttpRequestOptionsKey<int>("bazz"), out int intValue) && intValue == 100);

                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = JsonContent.Create(tunnel);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        tunnel = await client.GetTunnelAsync(tunnel, options, this.timeout);
        Assert.NotNull(tunnel);
        Assert.Equal(TunnelId, tunnel.TunnelId);
        Assert.Equal(ClusterId, tunnel.ClusterId);
    }

    [Fact]
    public async Task ReportProgress()
    {
        var options = new TunnelRequestOptions()
        {
            HttpRequestOptions = new Dictionary<string, object>
            {
                { "foo", "bar" },
                { "bazz", 100 },
            }
        };

        var tunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

       var handler = new MockHttpMessageHandler(
          (message, ct) =>
          {
              var result = new HttpResponseMessage(HttpStatusCode.OK);
              result.Content = JsonContent.Create(tunnel);
              return Task.FromResult(result);
          });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var list = new Queue<TunnelReportProgressEventArgs>();
        client.ReportProgress += (object sender, TunnelReportProgressEventArgs e) => {
            list.Enqueue(e);
        };

        await client.GetTunnelPortAsync(tunnel, 9900, options, this.timeout);

        Assert.Equal(TunnelProgress.StartingGetTunnelPort.ToString(), list.Dequeue().Progress);
        Assert.Equal(TunnelProgress.StartingRequestUri.ToString(), list.Dequeue().Progress);
        Assert.Equal(TunnelProgress.StartingRequestConfig.ToString(), list.Dequeue().Progress);
        Assert.Equal(TunnelProgress.StartingSendTunnelRequest.ToString(), list.Dequeue().Progress);
        Assert.Equal(TunnelProgress.CompletedSendTunnelRequest.ToString(), list.Dequeue().Progress);
        Assert.Equal(TunnelProgress.CompletedGetTunnelPort.ToString(), list.Dequeue().Progress);
    }

    [Fact]
    public async Task PreserveAccessTokens()
    {
        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
            AccessTokens = new Dictionary<string, string>
            {
                [TunnelAccessScopes.Manage] = "manage-token-1",
                [TunnelAccessScopes.Connect] = "connect-token-1",
            },
        };

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                var responseTunnel = new Tunnel
                {
                    TunnelId = TunnelId,
                    ClusterId = ClusterId,
                    AccessTokens = new Dictionary<string, string>
                    {
                        [TunnelAccessScopes.Manage] = "manage-token-2",
                        [TunnelAccessScopes.Host] = "host-token-2",
                    },
                };

                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = JsonContent.Create(responseTunnel);
                return Task.FromResult(result);
            });
        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var resultTunnel = await client.GetTunnelAsync(requestTunnel, options: null, this.timeout);
        Assert.NotNull(resultTunnel);
        Assert.NotNull(resultTunnel.AccessTokens);

        // Tokens in the request tunnel should be preserved, unless updated by the response.
        Assert.Collection(
            resultTunnel.AccessTokens.OrderBy((item) => item.Key),
            (item) => Assert.Equal(new KeyValuePair<string, string>(
                TunnelAccessScopes.Connect, "connect-token-1"), item), // preserved
            (item) => Assert.Equal(new KeyValuePair<string, string>(
                TunnelAccessScopes.Host, "host-token-2"), item),       // added
            (item) => Assert.Equal(new KeyValuePair<string, string>(
                TunnelAccessScopes.Manage, "manage-token-2"), item));  // updated
    }

    [Fact]
    public async Task CreateTunnelRetriesOnGeneratedIdConflict()
    {
        var requestTunnel = new Tunnel
        {
            ClusterId = ClusterId,

            // Tunnel ID is not set, so the client will generate one.
        };

        var callCount = 0;
        string firstTunnelId = null;
        string secondTunnelId = null;

        var handler = new MockHttpMessageHandler(
            async (message, ct) =>
            {
                callCount++;
                Assert.NotNull(message.Content);

                var sentTunnel = await message.Content!.ReadFromJsonAsync<Tunnel>(cancellationToken: ct);
                Assert.NotNull(sentTunnel);
                Assert.False(string.IsNullOrEmpty(sentTunnel!.TunnelId));

                if (callCount == 1)
                {
                    firstTunnelId = sentTunnel.TunnelId;
                    var conflictResult = new HttpResponseMessage(HttpStatusCode.Conflict);
                    conflictResult.RequestMessage = message;
                    return conflictResult;
                }

                secondTunnelId = sentTunnel.TunnelId;

                var responseTunnel = new Tunnel
                {
                    TunnelId = sentTunnel.TunnelId,
                    ClusterId = ClusterId,
                };

                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = JsonContent.Create(responseTunnel);
                return result;
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var resultTunnel = await client.CreateTunnelAsync(requestTunnel, options: null, this.timeout);

        Assert.Equal(2, callCount);
        Assert.NotNull(firstTunnelId);
        Assert.NotNull(secondTunnelId);
        Assert.Equal(secondTunnelId, resultTunnel.TunnelId);
        Assert.Equal(resultTunnel.TunnelId, requestTunnel.TunnelId);
    }

    [Fact]
    public async Task HandleFirewallResponse()
    {
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                var result = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    RequestMessage = message,
                };
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => client.GetTunnelAsync(requestTunnel, options: null, this.timeout));
        Assert.Contains("firewall", ex.Message);
        Assert.Contains(tunnelServiceUri.Host, ex.Message);
    }

    [Fact]
    public async Task HandlePolicyFailureResponse()
    {
        const string policyRequirement1 = "DisableAnonymousAccessRequirement";
        const string policyRequirement2 = "DisableAnonymousAccessRequirement";

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                var result = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    RequestMessage = message,
                };
                result.Headers.Add("X-Enterprise-Policy-Failure", policyRequirement1);
                result.Headers.Add("X-Enterprise-Policy-Failure", policyRequirement2);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => client.GetTunnelAsync(requestTunnel, options: null, this.timeout));
        Assert.Collection(
            ex.GetEnterprisePolicyRequirements(),
            (r) => Assert.Equal(policyRequirement1, r),
            (r) => Assert.Equal(policyRequirement2, r));
    }

    [Fact]
    public async Task CustomDomainDoesNotModifyHostname()
    {
        Uri capturedUri = null;
        var tunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                capturedUri = message.RequestUri;
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = JsonContent.Create(tunnel);
                return Task.FromResult(result);
            });

        var client = TunnelManagementClient.ForCustomDomain(
            "app.github.dev",
            new[] { this.userAgent },
            httpHandler: handler);

        await client.GetTunnelAsync(tunnel, options: null, this.timeout);

        Assert.NotNull(capturedUri);
        Assert.Equal("cp.app.github.dev", capturedUri.Host);
    }

    [Fact]
    public async Task StandardServiceUriReplacesClusterIdInHostname()
    {
        Uri capturedUri = null;
        var tunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = ClusterId,
        };

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                capturedUri = message.RequestUri;
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = JsonContent.Create(tunnel);
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(
            this.userAgent,
            tunnelServiceUri: new Uri("https://global.rel.tunnels.api.visualstudio.com/"),
            httpHandler: handler);

        await client.GetTunnelAsync(tunnel, options: null, this.timeout);

        Assert.NotNull(capturedUri);
        Assert.StartsWith($"{ClusterId}.", capturedUri.Host);
    }


    [Fact]
    public async Task GetClusterRecommendationsAsync_ReturnsDeserializedResponse()
    {
        const string responseJson = @"{
            ""preferredClusterId"": ""usw2"",
            ""recommendedClusterId"": ""usw4"",
            ""isFallback"": true,
            ""recommendations"": [
                {
                    ""clusterId"": ""usw4"",
                    ""azureLocation"": ""WestUs2"",
                    ""azureGeo"": ""United States"",
                    ""clusterUri"": ""https://usw4.ci.tunnels.dev.api.visualstudio.com"",
                    ""availability"": ""Available"",
                    ""utilizationPercent"": 12.5,
                    ""reason"": ""Preferred cluster available""
                },
                {
                    ""clusterId"": ""usw2"",
                    ""azureLocation"": ""WestUs2"",
                    ""azureGeo"": ""United States"",
                    ""clusterUri"": ""https://usw2.ci.tunnels.dev.api.visualstudio.com"",
                    ""availability"": ""Degraded"",
                    ""utilizationPercent"": 87.0,
                    ""reason"": ""Near capacity""
                }
            ]
        }";

        Uri capturedUri = null;
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                capturedUri = message.RequestUri;
                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = new StringContent(
                        responseJson, System.Text.Encoding.UTF8, "application/json"),
                };
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        var response = await client.GetClusterRecommendationsAsync(cancellation: this.timeout);

        Assert.NotNull(response);
        Assert.Equal("usw2", response.PreferredClusterId);
        Assert.Equal("usw4", response.RecommendedClusterId);
        Assert.True(response.IsFallback);
        Assert.Equal(2, response.Recommendations.Length);
        Assert.Equal("usw4", response.Recommendations[0].ClusterId);
        Assert.Equal(ClusterAvailability.Available, response.Recommendations[0].Availability);
        Assert.Equal(12.5, response.Recommendations[0].UtilizationPercent);
        Assert.Equal(ClusterAvailability.Degraded, response.Recommendations[1].Availability);
        Assert.NotNull(capturedUri);
        Assert.Contains("/clusters/recommendations", capturedUri.AbsolutePath);
    }

    [Fact]
    public async Task GetClusterRecommendationsAsync_PassesQueryParameters()
    {
        Uri capturedUri = null;
        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                capturedUri = message.RequestUri;
                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = new StringContent(
                        "{\"recommendations\":[]}",
                        System.Text.Encoding.UTF8,
                        "application/json"),
                };
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(this.userAgent, null, this.tunnelServiceUri, handler);

        await client.GetClusterRecommendationsAsync(
            preferredClusterId: "usw2", requiredGeo: "us", cancellation: this.timeout);

        Assert.NotNull(capturedUri);
        Assert.Contains("preferredClusterId=usw2", capturedUri.Query);
        Assert.Contains("requiredGeo=us", capturedUri.Query);
    }

    [Fact]
    public async Task CreateTunnelAsync_AutoRecommendsWhenClusterIdNotSet()
    {
        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,

            // ClusterId intentionally not set, so the client auto-recommends.
        };

        var recommendationCalls = 0;
        var createCalls = 0;
        Uri createUri = null;

        var handler = new MockHttpMessageHandler(
            async (message, ct) =>
            {
                if (message.RequestUri!.AbsolutePath.EndsWith("/recommendations"))
                {
                    recommendationCalls++;
                    var recResult = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = message,
                        Content = new StringContent(
                            "{\"recommendedClusterId\":\"usw4\",\"recommendations\":[]}",
                            System.Text.Encoding.UTF8,
                            "application/json"),
                    };
                    return recResult;
                }

                createCalls++;
                createUri = message.RequestUri;
                var sentTunnel = await message.Content!.ReadFromJsonAsync<Tunnel>(
                    cancellationToken: ct);
                var responseTunnel = new Tunnel
                {
                    TunnelId = sentTunnel!.TunnelId,
                    ClusterId = "usw4",
                };
                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = JsonContent.Create(responseTunnel),
                };
                return result;
            });

        var client = new TunnelManagementClient(
            this.userAgent,
            tunnelServiceUri: new Uri("https://global.rel.tunnels.api.visualstudio.com/"),
            httpHandler: handler);

        var resultTunnel = await client.CreateTunnelAsync(
            requestTunnel, options: null, this.timeout);

        Assert.Equal(1, recommendationCalls);
        Assert.Equal(1, createCalls);
        Assert.Equal("usw4", requestTunnel.ClusterId);
        Assert.NotNull(createUri);
        Assert.StartsWith("usw4.", createUri.Host);
        Assert.NotNull(resultTunnel);
    }

    [Fact]
    public async Task CreateTunnelAsync_ForwardsRequiredGeoFromOptionsToRecommendation()
    {
        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,

            // ClusterId intentionally not set, so the client auto-recommends.
        };

        Uri recommendationUri = null;
        Uri createUri = null;

        var handler = new MockHttpMessageHandler(
            async (message, ct) =>
            {
                if (message.RequestUri!.AbsolutePath.EndsWith("/recommendations"))
                {
                    recommendationUri = message.RequestUri;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = message,
                        Content = new StringContent(
                            "{\"recommendedClusterId\":\"usw4\",\"recommendations\":[]}",
                            System.Text.Encoding.UTF8,
                            "application/json"),
                    };
                }

                createUri = message.RequestUri;
                var sentTunnel = await message.Content!.ReadFromJsonAsync<Tunnel>(
                    cancellationToken: ct);
                var responseTunnel = new Tunnel
                {
                    TunnelId = sentTunnel!.TunnelId,
                    ClusterId = "usw4",
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = JsonContent.Create(responseTunnel),
                };
            });

        var client = new TunnelManagementClient(
            this.userAgent,
            tunnelServiceUri: new Uri("https://global.rel.tunnels.api.visualstudio.com/"),
            httpHandler: handler);

        var options = new TunnelRequestOptions { RequiredGeo = "us" };
        await client.CreateTunnelAsync(requestTunnel, options, this.timeout);

        // requiredGeo flows to the recommendations request...
        Assert.NotNull(recommendationUri);
        Assert.Contains("requiredGeo=us", recommendationUri.Query);

        // ...but is NOT included on the create-tunnel request itself.
        Assert.NotNull(createUri);
        Assert.DoesNotContain("requiredGeo", createUri.Query);
    }

    [Fact]
    public async Task CreateTunnelAsync_SkipsRecommendWhenClusterIdSet()
    {
        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,
            ClusterId = "usw2",
        };

        var recommendationCalls = 0;
        var createCalls = 0;
        Uri createUri = null;

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                if (message.RequestUri!.AbsolutePath.EndsWith("/recommendations"))
                {
                    recommendationCalls++;
                }
                else
                {
                    createCalls++;
                    createUri = message.RequestUri;
                }

                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = JsonContent.Create(requestTunnel),
                };
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(
            this.userAgent,
            tunnelServiceUri: new Uri("https://global.rel.tunnels.api.visualstudio.com/"),
            httpHandler: handler);

        await client.CreateTunnelAsync(requestTunnel, options: null, this.timeout);

        Assert.Equal(0, recommendationCalls);
        Assert.Equal(1, createCalls);
        Assert.NotNull(createUri);
        Assert.StartsWith("usw2.", createUri.Host);
    }

    [Fact]
    public async Task CreateTunnelAsync_FallsBackOnRecommendFailure()
    {
        var requestTunnel = new Tunnel
        {
            TunnelId = TunnelId,

            // ClusterId not set; recommendations call will fail.
        };

        var createCalls = 0;
        Uri createUri = null;

        var handler = new MockHttpMessageHandler(
            (message, ct) =>
            {
                if (message.RequestUri!.AbsolutePath.EndsWith("/recommendations"))
                {
                    var failure = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        RequestMessage = message,
                    };
                    return Task.FromResult(failure);
                }

                createCalls++;
                createUri = message.RequestUri;
                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = message,
                    Content = JsonContent.Create(requestTunnel),
                };
                return Task.FromResult(result);
            });

        var client = new TunnelManagementClient(
            this.userAgent,
            tunnelServiceUri: new Uri("https://global.rel.tunnels.api.visualstudio.com/"),
            httpHandler: handler);

        var resultTunnel = await client.CreateTunnelAsync(
            requestTunnel, options: null, this.timeout);

        Assert.Equal(1, createCalls);
        Assert.Null(requestTunnel.ClusterId);
        Assert.NotNull(createUri);

        // No cluster prefix was added: routing falls back to the global hostname.
        Assert.Equal("global.rel.tunnels.api.visualstudio.com", createUri.Host);
        Assert.NotNull(resultTunnel);
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

