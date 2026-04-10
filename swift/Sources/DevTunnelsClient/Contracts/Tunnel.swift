/// Data contract for tunnel objects managed through the tunnel service REST API.
public struct Tunnel: Codable, Equatable, Sendable {
    /// Cluster ID where the tunnel was created.
    public var clusterId: String?

    /// Generated unique tunnel ID within the cluster.
    public var tunnelId: String?

    /// Optional short name (alias). Globally unique within the parent domain.
    public var name: String?

    /// Description of the tunnel.
    public var description: String?

    /// Labels for the tunnel.
    public var labels: [String]?

    /// Optional parent domain (if not using the default).
    public var domain: String?

    /// Dictionary mapping from scopes to tunnel access tokens.
    public var accessTokens: [String: String]?

    /// Current connection status of the tunnel.
    public var status: TunnelStatus?

    /// Endpoints where hosts are currently accepting client connections.
    public var endpoints: [TunnelEndpoint]?

    /// Ports in the tunnel.
    public var ports: [TunnelPort]?

    public init(
        clusterId: String? = nil,
        tunnelId: String? = nil,
        name: String? = nil,
        description: String? = nil,
        labels: [String]? = nil,
        domain: String? = nil,
        accessTokens: [String: String]? = nil,
        status: TunnelStatus? = nil,
        endpoints: [TunnelEndpoint]? = nil,
        ports: [TunnelPort]? = nil
    ) {
        self.clusterId = clusterId
        self.tunnelId = tunnelId
        self.name = name
        self.description = description
        self.labels = labels
        self.domain = domain
        self.accessTokens = accessTokens
        self.status = status
        self.endpoints = endpoints
        self.ports = ports
    }
}
