using Xunit;

namespace Microsoft.VsSaaS.TunnelService.Test.Mocks;

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

    public Task<Stream> CreateRelayStreamAsync(
        Uri relayUri,
        string accessToken,
        string connectionType,
        CancellationToken cancellation)
    {
        Assert.NotNull(relayUri);
        Assert.NotNull(accessToken);
        Assert.Equal(this.connectionType, connectionType);

        return StreamFactory(accessToken);
    }
}
