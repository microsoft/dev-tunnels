using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Events;
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

        var list = new Queue<SshReportProgressEventArgs>();
        client.ReportProgress += (object sender, SshReportProgressEventArgs e) => {
            list.Enqueue(e);
        };

        await client.GetTunnelPortAsync(tunnel, 9900, options, this.timeout);

        Assert.Equal(Progress.StartingGetTunnelPort, list.Dequeue().Progress);
        Assert.Equal(Progress.StartingRequestUri, list.Dequeue().Progress);
        Assert.Equal(Progress.StartingRequestConfig, list.Dequeue().Progress);
        Assert.Equal(Progress.StartingSendTunnelRequest, list.Dequeue().Progress);
        Assert.Equal(Progress.CompletedSendTunnelRequest, list.Dequeue().Progress);
        Assert.Equal(Progress.CompletedGetTunnelPort, list.Dequeue().Progress);WW
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

