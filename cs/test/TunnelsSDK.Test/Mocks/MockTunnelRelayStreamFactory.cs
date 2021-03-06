
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VsSaaS.TunnelService.Test.Mocks;

public class MockTunnelRelayStreamFactory : ITunnelRelayStreamFactory
{
    private readonly string connectionType;
    private readonly Stream stream;

    public MockTunnelRelayStreamFactory(string connectionType, Stream stream)
    {
        this.connectionType = connectionType;
        this.stream = stream;
    }

    public Task<Stream> CreateRelayStreamAsync(
        Uri relayUri,
        string accessToken,
        string connectionType,
        CancellationToken cancellation)
    {
        Assert.NotNull(relayUri);
        Assert.NotNull(accessToken);
        Assert.Equal(this.connectionType, connectionType);

        return Task.FromResult(stream);
    }
}
