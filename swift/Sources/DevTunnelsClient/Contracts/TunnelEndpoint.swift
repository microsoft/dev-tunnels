/// Base class for tunnel connection parameters.
///
/// A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
public struct TunnelEndpoint: Codable, Equatable, Sendable {
    /// Endpoint ID.
    public var id: String?

    /// Connection mode of the endpoint.
    public var connectionMode: TunnelConnectionMode?

    /// ID of the host listening on this endpoint.
    public var hostId: String?

    /// Public keys for authenticating the host.
    public var hostPublicKeys: [String]?

    /// URI format string for web client port connections.
    /// Contains `{port}` token to be replaced with actual port number.
    public var portUriFormat: String?

    /// URI for web client connection to the default port.
    public var tunnelUri: String?

    /// Host relay URI (for TunnelRelay connection mode).
    public var hostRelayUri: String?

    /// Client relay URI (for TunnelRelay connection mode).
    public var clientRelayUri: String?

    public init(
        id: String? = nil,
        connectionMode: TunnelConnectionMode? = nil,
        hostId: String? = nil,
        hostPublicKeys: [String]? = nil,
        portUriFormat: String? = nil,
        tunnelUri: String? = nil,
        hostRelayUri: String? = nil,
        clientRelayUri: String? = nil
    ) {
        self.id = id
        self.connectionMode = connectionMode
        self.hostId = hostId
        self.hostPublicKeys = hostPublicKeys
        self.portUriFormat = portUriFormat
        self.tunnelUri = tunnelUri
        self.hostRelayUri = hostRelayUri
        self.clientRelayUri = clientRelayUri
    }
}

/// Token in `portUriFormat` to be replaced by a port number.
public let tunnelEndpointPortToken = "{port}"
