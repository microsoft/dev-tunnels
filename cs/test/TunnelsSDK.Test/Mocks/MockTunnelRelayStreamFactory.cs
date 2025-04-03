using Microsoft.DevTunnels.Connections;
using System.Diagnostics;
using Xunit;

namespace Microsoft.DevTunnels.Test.Mocks;

public class MockTunnelRelayStreamFactory : ITunnelRelayStreamFactory
{
    private readonly string connectionType;
    private readonly Stream stream;

    public MockTunnelRelayStreamFactory(string connectionType, Stream stream = null)
    {
        this.connectionType = connectionType;
        this.stream = stream;
        StreamFactory = (string accessToken) => Task.FromResult(this.stream);
    }

    public Func<string, Task<Stream>> StreamFactory { get; set; }

    public async Task<(Stream, string)> CreateRelayStreamAsync(
        Uri relayUri,
        string accessToken,
        string[] subprotocols,
        TraceSource trace,
        CancellationToken cancellation)
    {
        Assert.NotNull(relayUri);
        Assert.NotNull(accessToken);
        Assert.Contains(this.connectionType, subprotocols);

        var stream = await StreamFactory(accessToken);
        return (stream, this.connectionType);
    }
}
