import Foundation
import NIOSSH

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

    /// Optional SSH host key validator.
    ///
    /// Invoked during the SSH handshake with the server's `NIOSSHPublicKey`.
    /// Return `true` to accept the key, `false` to reject and abort the
    /// connection. Callers can compare the supplied key against a known
    /// `NIOSSHPublicKey` (parsed via `init(openSSHPublicKey:)`) using its
    /// `Hashable` conformance for pinning.
    ///
    /// **Security note:** When this is `nil` (the default), the SDK accepts
    /// **any** SSH host key presented by the relay — connection security relies
    /// entirely on the WebSocket TLS layer and the tunnel access token. This
    /// matches the behavior of the Go SDK's `InsecureIgnoreHostKey`, but means
    /// a man-in-the-middle who can present a valid TLS certificate (e.g. via a
    /// compromised CA) could intercept tunnel traffic. Pass a validator that
    /// pins known fingerprints for stronger defense in depth.
    public let hostKeyValidator: (@Sendable (_ hostKey: NIOSSHPublicKey) -> Bool)?

    public init(
        relayUri: String,
        accessToken: String,
        port: UInt16,
        subprotocol: String = TunnelRelayConstants.clientWebSocketSubProtocol,
        connectionTimeout: TimeInterval = 30,
        keepaliveInterval: TimeInterval = TunnelRelayConstants.defaultKeepaliveInterval,
        hostKeyValidator: (@Sendable (_ hostKey: NIOSSHPublicKey) -> Bool)? = nil
    ) {
        self.relayUri = relayUri
        self.accessToken = accessToken
        self.port = port
        self.subprotocol = subprotocol
        self.connectionTimeout = connectionTimeout
        self.keepaliveInterval = keepaliveInterval
        self.hostKeyValidator = hostKeyValidator
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
    ///
    /// Uses a case-insensitive prefix check (not substring) so that tokens which
    /// happen to contain the substring "tunnel" anywhere in their body are not
    /// mistakenly treated as already-prefixed.
    var authorizationHeader: String {
        let trimmed = accessToken.trimmingCharacters(in: .whitespaces)
        if trimmed.lowercased().hasPrefix("tunnel ") {
            return trimmed
        }
        return "Tunnel \(trimmed)"
    }

    /// Equatable conformance ignores `hostKeyValidator` (closures are not equatable).
    public static func == (lhs: TunnelRelayConfig, rhs: TunnelRelayConfig) -> Bool {
        lhs.relayUri == rhs.relayUri
            && lhs.accessToken == rhs.accessToken
            && lhs.port == rhs.port
            && lhs.subprotocol == rhs.subprotocol
            && lhs.connectionTimeout == rhs.connectionTimeout
            && lhs.keepaliveInterval == rhs.keepaliveInterval
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
