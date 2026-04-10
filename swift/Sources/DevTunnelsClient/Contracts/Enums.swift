/// Defines the connection mode for a tunnel endpoint.
public enum TunnelConnectionMode: String, Codable, Sendable {
    /// Connections via a local network address.
    case localNetwork = "LocalNetwork"

    /// Connections via the tunnel service's built-in relay.
    case tunnelRelay = "TunnelRelay"

    /// Unknown connection mode from the service.
    case unknown

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        let rawValue = try container.decode(String.self)
        self = TunnelConnectionMode(rawValue: rawValue) ?? .unknown
    }
}

/// Defines scopes for tunnel access tokens.
public enum TunnelAccessScopes {
    /// Create tunnels.
    public static let create = "create"
    /// Manage tunnel properties.
    public static let manage = "manage"
    /// Manage tunnel ports.
    public static let managePorts = "manage:ports"
    /// Host connections.
    public static let host = "host"
    /// Inspect tunnel activity.
    public static let inspect = "inspect"
    /// Connect to tunnel ports.
    public static let connect = "connect"
}

/// Protocol hint for a tunnel port.
///
/// Indicates the expected application protocol for the tunnel port.
/// The service uses this to generate appropriate access URLs.
public enum TunnelPortProtocol: String, Codable, Sendable {
    /// Automatically detect the protocol.
    case auto
    /// HTTP protocol.
    case http
    /// HTTPS protocol.
    case https
    /// Remote Desktop Protocol.
    case rdp
    /// Secure Shell protocol.
    case ssh

    /// Unknown protocol from the service.
    case unknown

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        let rawValue = try container.decode(String.self)
        self = TunnelPortProtocol(rawValue: rawValue) ?? .unknown
    }
}
