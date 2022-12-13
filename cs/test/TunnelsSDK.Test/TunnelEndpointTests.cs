using System.Text.Json;
using FluentAssertions;
using Microsoft.DevTunnels.Contracts;
using Xunit;

namespace Microsoft.DevTunnels.Test;

public class TunnelEndpointTests
{
    public static IEnumerable<object[]> Endpoints { get; } = new []
    {
        new TunnelRelayTunnelEndpoint(),
        new TunnelRelayTunnelEndpoint()
        {
            PortUris = new Dictionary<int, string[]>
            {
                [100] = new[] { "https://tnnl0001-100.devtunnels.ms/", "https://tnnl0001.devtunnels.ms:100/" },
                [3000] = new[] { "https://tnnl0001.devtunnels.ms:3000/" },
            },
        },
    }
    .Select((ep) => new object[] { ep });

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void SerializeDeserializeEndpoint(TunnelEndpoint endpoint)
    {
        var content = JsonSerializer.Serialize(endpoint, TunnelContracts.JsonOptions);
        var newEndpoint = JsonSerializer.Deserialize<TunnelEndpoint>(content, TunnelContracts.JsonOptions);
        if (endpoint.PortUris?.Count > 0)
        {
            newEndpoint.PortUris.Should().NotBeNull();
            newEndpoint.PortUris.Should().HaveCount(endpoint.PortUris.Count);
            foreach (var kvp in endpoint.PortUris)
            {
                newEndpoint.PortUris.Should().ContainKey(kvp.Key);
                var values = newEndpoint.PortUris[kvp.Key];
                values.Should().Equal(kvp.Value);
            }
        }
        else
        {
            newEndpoint.PortUris.Should().BeNull();
        }
    }
}
