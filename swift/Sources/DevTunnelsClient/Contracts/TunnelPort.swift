/// Data contract for tunnel port objects managed through the tunnel service REST API.
public struct TunnelPort: Codable, Equatable, Sendable {
    /// Cluster ID where the tunnel was created.
    public var clusterId: String?

    /// Generated tunnel ID, unique within the cluster.
    public var tunnelId: String?

    /// IP port number of the tunnel port.
    public var portNumber: UInt16

    /// Optional short name of the port. Unique among named ports of the same tunnel.
    public var name: String?

    /// Optional description of the port.
    public var description: String?

    /// Protocol of the tunnel port (auto, http, https, rdp, ssh).
    public var `protocol`: TunnelPortProtocol?

    /// Dictionary mapping from scopes to port-level access tokens.
    public var accessTokens: [String: String]?

    public init(
        clusterId: String? = nil,
        tunnelId: String? = nil,
        portNumber: UInt16,
        name: String? = nil,
        description: String? = nil,
        protocol: TunnelPortProtocol? = nil,
        accessTokens: [String: String]? = nil
    ) {
        self.clusterId = clusterId
        self.tunnelId = tunnelId
        self.portNumber = portNumber
        self.name = name
        self.description = description
        self.protocol = `protocol`
        self.accessTokens = accessTokens
    }
}
