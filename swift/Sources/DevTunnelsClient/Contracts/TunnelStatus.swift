/// Current status of a tunnel.
public struct TunnelStatus: Codable, Equatable, Sendable {
    /// Current value and limit for the number of ports on the tunnel.
    public var portCount: ResourceStatus?

    /// Current value and limit for the number of hosts connected.
    public var hostConnectionCount: ResourceStatus?

    /// UTC time when a host was last accepting connections, or nil if never.
    public var lastHostConnectionTime: String?

    /// Current value and limit for the number of clients connected.
    public var clientConnectionCount: ResourceStatus?

    public init(
        portCount: ResourceStatus? = nil,
        hostConnectionCount: ResourceStatus? = nil,
        lastHostConnectionTime: String? = nil,
        clientConnectionCount: ResourceStatus? = nil
    ) {
        self.portCount = portCount
        self.hostConnectionCount = hostConnectionCount
        self.lastHostConnectionTime = lastHostConnectionTime
        self.clientConnectionCount = clientConnectionCount
    }
}

/// Current value and limit for a limited resource related to a tunnel or port.
/// The API may return this as either a plain number or an object with `current` and `limit`.
public struct ResourceStatus: Codable, Equatable, Sendable {
    /// Current count of the resource (e.g., connected clients, open ports).
    public var current: UInt64

    /// Maximum allowed by the service, or nil if unlimited.
    public var limit: UInt64?

    public init(current: UInt64 = 0, limit: UInt64? = nil) {
        self.current = current
        self.limit = limit
    }

    public init(from decoder: Decoder) throws {
        // The API can return either a plain number or {current, limit} object.
        if let container = try? decoder.singleValueContainer(),
           let value = try? container.decode(UInt64.self) {
            self.current = value
            self.limit = nil
        } else {
            let container = try decoder.container(keyedBy: CodingKeys.self)
            self.current = try container.decodeIfPresent(UInt64.self, forKey: .current) ?? 0
            self.limit = try container.decodeIfPresent(UInt64.self, forKey: .limit)
        }
    }

    private enum CodingKeys: String, CodingKey {
        case current, limit
    }
}
