import Foundation

/// Configuration for a tunnel relay connection.
public struct TunnelRelayConfig: Sendable, Equatable {
    /// Client relay URI from the tunnel endpoint (wss://...).
    public let relayUri: String

    /// Tunnel access token with "connect" scope.
    public let accessToken: String

    /// The port to forward to on the remote host.
    public let port: UInt16

    /// WebSocket subprotocol for the relay.
    public let subprotocol: String

    /// Connection timeout in seconds.
    public let connectionTimeout: TimeInterval

    /// Interval in seconds between WebSocket keepalive pings. Set to 0 to disable.
    public let keepaliveInterval: TimeInterval

    public init(
        relayUri: String,
        accessToken: String,
        port: UInt16,
        subprotocol: String = TunnelRelayConstants.clientWebSocketSubProtocol,
        connectionTimeout: TimeInterval = 30,
        keepaliveInterval: TimeInterval = TunnelRelayConstants.defaultKeepaliveInterval
    ) {
        self.relayUri = relayUri
        self.accessToken = accessToken
        self.port = port
        self.subprotocol = subprotocol
        self.connectionTimeout = connectionTimeout
        self.keepaliveInterval = keepaliveInterval
    }

    /// Validates that the config has all required fields.
    public func validate() -> TunnelRelayConfigError? {
        if relayUri.isEmpty {
            return .missingRelayUri
        }
        guard let url = URL(string: relayUri) else {
            return .invalidRelayUri(relayUri)
        }
        guard url.scheme == "wss" || url.scheme == "ws" else {
            return .invalidRelayUri(relayUri)
        }
        if accessToken.isEmpty {
            return .missingAccessToken
        }
        if port == 0 {
            return .invalidPort
        }
        return nil
    }

    /// Builds the Authorization header value.
    /// Prefixes "Tunnel " if not already present.
    var authorizationHeader: String {
        if accessToken.contains("Tunnel") || accessToken.contains("tunnel") {
            return accessToken
        }
        return "Tunnel \(accessToken)"
    }
}

/// Errors from config validation.
public enum TunnelRelayConfigError: Error, Equatable {
    case missingRelayUri
    case invalidRelayUri(String)
    case missingAccessToken
    case invalidPort
}

/// Constants for the tunnel relay protocol.
public enum TunnelRelayConstants {
    /// V1 WebSocket subprotocol for client relay connections.
    public static let clientWebSocketSubProtocol = "tunnel-relay-client"

    /// SSH channel type for port forwarding.
    public static let portForwardChannelType = "forwarded-tcpip"

    /// SSH global request type for port forwarding notification.
    public static let portForwardRequestType = "tcpip-forward"

    /// SSH user for tunnel connections.
    public static let sshUser = "tunnel"

    /// Default interval (seconds) between WebSocket keepalive pings.
    public static let defaultKeepaliveInterval: TimeInterval = 30
}
