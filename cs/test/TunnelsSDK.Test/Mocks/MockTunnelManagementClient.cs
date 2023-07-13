using Microsoft.DevTunnels.Contracts;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Test.Mocks;

public class MockTunnelManagementClient : ITunnelManagementClient
{
    private uint idCounter = 0;
    public IList<Tunnel> Tunnels { get; } = new List<Tunnel>();

    public string HostRelayUri { get; set; }

    public string ClientRelayUri { get; set; }

    public ICollection<TunnelAccessSubject> KnownSubjects { get; }
        = new List<TunnelAccessSubject>();

    public Task<Tunnel[]> ListTunnelsAsync(
        string clusterId,
        string domain,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        IEnumerable<Tunnel> tunnels = Tunnels;

        domain ??= string.Empty;
        tunnels = tunnels.Where((t) => (t.Domain ?? string.Empty) == domain);

        return Task.FromResult(tunnels.ToArray());
    }

    public Task<Tunnel> GetTunnelAsync(
        Tunnel tunnel,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        string clusterId = tunnel.ClusterId;
        string tunnelId = tunnel.TunnelId;
        string name = tunnel.Name;

        tunnel = Tunnels.FirstOrDefault((t) =>
            !string.IsNullOrEmpty(name) ? t.Name == name || t.TunnelId == name :
            t.ClusterId == clusterId && t.TunnelId == tunnelId);

        IssueMockTokens(tunnel, options);
        return Task.FromResult(tunnel);
    }

    public async Task<Tunnel> CreateTunnelAsync(
        Tunnel tunnel,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        if ((await GetTunnelAsync(tunnel, options, cancellation)) != null)
        {
            throw new InvalidOperationException("Tunnel already exists.");
        }

        tunnel.TunnelId = "tunnel" + (++this.idCounter);
        tunnel.ClusterId = "localhost";
        Tunnels.Add(tunnel);

        IssueMockTokens(tunnel, options);
        return tunnel;
    }

    public Task<Tunnel> UpdateTunnelAsync(
        Tunnel tunnel,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        foreach (var t in Tunnels)
        {
            if (t.ClusterId == tunnel.ClusterId && t.TunnelId == tunnel.TunnelId)
            {
                if (tunnel.Name != null)
                {
                    t.Name = tunnel.Name;
                }

                if (tunnel.Options != null)
                {
                    t.Options = tunnel.Options;
                }

                if (tunnel.AccessControl != null)
                {
                    t.AccessControl = tunnel.AccessControl;
                }
            }
        }

        IssueMockTokens(tunnel, options);

        return Task.FromResult(tunnel);
    }

    public Task<bool> DeleteTunnelAsync(
        Tunnel tunnel,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        for (var i = 0; i < Tunnels.Count; i++)
        {
            var t = Tunnels[i];
            if (t.ClusterId == tunnel.ClusterId && t.TunnelId == tunnel.TunnelId)
            {
                Tunnels.RemoveAt(i);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public Task<TunnelEndpoint> UpdateTunnelEndpointAsync(
        Tunnel tunnel,
        TunnelEndpoint endpoint,
        TunnelRequestOptions options = null,
        CancellationToken cancellation = default)
    {
        tunnel.Endpoints ??= Array.Empty<TunnelEndpoint>();

        for (int i = 0; i < tunnel.Endpoints.Length; i++)
        {
            if (tunnel.Endpoints[i].HostId == endpoint.HostId &&
                tunnel.Endpoints[i].ConnectionMode == endpoint.ConnectionMode)
            {
                tunnel.Endpoints[i] = endpoint;
                return Task.FromResult(endpoint);
            }
        }

        var newArray = new TunnelEndpoint[tunnel.Endpoints.Length + 1];
        Array.Copy(tunnel.Endpoints, newArray, tunnel.Endpoints.Length);
        newArray[newArray.Length - 1] = endpoint;
        tunnel.Endpoints = newArray;

        if (endpoint is TunnelRelayTunnelEndpoint tunnelRelayEndpoint)
        {
            const string TunnelIdUriToken = "{tunnelId}";

            tunnelRelayEndpoint.HostRelayUri = HostRelayUri?
                .Replace(TunnelIdUriToken, tunnel.TunnelId);
            tunnelRelayEndpoint.ClientRelayUri = ClientRelayUri?
                .Replace(TunnelIdUriToken, tunnel.TunnelId);
        }

        return Task.FromResult(endpoint);
    }

    public Task<bool> DeleteTunnelEndpointsAsync(
        Tunnel tunnel,
        string hostId,
        TunnelConnectionMode? connectionMode,
        TunnelRequestOptions options = null,
        CancellationToken cancellation = default)
    {
        Requires.NotNullOrEmpty(hostId, nameof(hostId));

        if (tunnel.Endpoints == null)
        {
            return Task.FromResult(false);
        }

        var initialLength = tunnel.Endpoints.Length;
        tunnel.Endpoints = tunnel.Endpoints
            .Where((ep) => ep.HostId == hostId &&
                (connectionMode == null || ep.ConnectionMode == connectionMode))
            .ToArray();
        return Task.FromResult(tunnel.Endpoints.Length < initialLength);
    }


    public Task<TunnelPort[]> ListTunnelPortsAsync(
        Tunnel tunnel,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public Task<TunnelPort> GetTunnelPortAsync(
        Tunnel tunnel,
        ushort portNumber,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public Task<TunnelPort> CreateTunnelPortAsync(
        Tunnel tunnel,
        TunnelPort tunnelPort,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        tunnelPort = new TunnelPort
        {
            TunnelId = tunnel.TunnelId,
            ClusterId = tunnel.ClusterId,
            PortNumber = tunnelPort.PortNumber,
            Protocol = tunnelPort.Protocol,
            IsDefault = tunnelPort.IsDefault,
            AccessControl = tunnelPort.AccessControl,
            Options = tunnelPort.Options,
            SshUser = tunnelPort.SshUser,
        };
        tunnel.Ports = (tunnel.Ports ?? Enumerable.Empty<TunnelPort>())
            .Concat(new[] { tunnelPort }).ToArray();
        return Task.FromResult(tunnelPort);
    }

    public Task<TunnelPort> UpdateTunnelPortAsync(
        Tunnel tunnel,
        TunnelPort tunnelPort,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteTunnelPortAsync(
        Tunnel tunnel,
        ushort portNumber,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        var tunnelPort = tunnel.Ports?.FirstOrDefault((p) => p.PortNumber == portNumber);
        if (tunnelPort == null)
        {
            return Task.FromResult(false);
        }

        tunnel.Ports = tunnel.Ports.Where((p) => p != tunnelPort).ToArray();
        return Task.FromResult(true);
    }

    public Task<Tunnel[]> SearchTunnelsAsync(
        string[] tags,
        bool requireAllTags,
        string clusterId,
        string domain,
        TunnelRequestOptions options,
        CancellationToken cancellation)
    {
        IEnumerable<Tunnel> tunnels;
        if (!requireAllTags)
        {
            tunnels = Tunnels.Where(tunnel => (tunnel.Tags != null) && (tunnel.Tags.Intersect(tags).Count() > 0));
        }
        else
        {
            var numTags = tags.Length;
            tunnels = Tunnels.Where(tunnel => (tunnel.Tags != null) && (tunnel.Tags.Intersect(tags).Count() == numTags));
        }

        domain ??= string.Empty;
        tunnels = tunnels.Where((t) => (t.Domain ?? string.Empty) == domain);

        return Task.FromResult(tunnels.ToArray());
    }

    public Task<TunnelAccessSubject[]> FormatSubjectsAsync(
        TunnelAccessSubject[] subjects,
        TunnelRequestOptions options,
        CancellationToken cancellation = default)
    {
        var formattedSubjects = new List<TunnelAccessSubject>(subjects.Length);

        foreach (var subject in subjects)
        {
            var knownSubject = KnownSubjects.FirstOrDefault((s) => s.Id == subject.Id);
            formattedSubjects.Add(knownSubject ?? subject);
        }

        return Task.FromResult(formattedSubjects.ToArray());
    }

    public Task<TunnelAccessSubject[]> ResolveSubjectsAsync(
        TunnelAccessSubject[] subjects,
        TunnelRequestOptions options,
        CancellationToken cancellation = default)
    {
        var resolvedSubjects = new List<TunnelAccessSubject>(subjects.Length);

        foreach (var subject in subjects)
        {
            var matchingSubjects = KnownSubjects
                .Where((s) => s.Name.Contains(subject.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchingSubjects.Length == 0)
            {
                resolvedSubjects.Add(subject);
            }
            else if (matchingSubjects.Length == 1)
            {
                resolvedSubjects.Add(matchingSubjects[0]);
            }
            else
            {
                subject.Matches = matchingSubjects;
                resolvedSubjects.Add(subject);
            }
        }

        return Task.FromResult(resolvedSubjects.ToArray());
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private static void IssueMockTokens(Tunnel tunnel, TunnelRequestOptions options)
    {
        if (tunnel != null && options?.TokenScopes != null)
        {
            tunnel.AccessTokens = new Dictionary<string, string>();
            foreach (var scope in options.TokenScopes)
            {
                tunnel.AccessTokens[scope] = "mock-token";
            }
        }
    }

    public Task<ClusterDetails[]> ListClustersAsync(CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckNameAvailabilityAsync(string name, CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }

    public Task<NamedRateStatus[]> ListUserLimitsAsync(CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }
}
