import Foundation

/// Connection state for a tunnel relay client.
public enum RelayConnectionState: Equatable, Sendable {
    /// Not connected.
    case disconnected

    /// WebSocket connection in progress.
    case connectingWebSocket

    /// WebSocket connected, SSH handshake in progress.
    case connectingSSH

    /// SSH connected, opening port forwarding channel.
    case openingChannel

    /// Fully connected and streaming data.
    case connected

    /// Connection lost, attempting to reconnect.
    case reconnecting(attempt: Int)

    /// Connection failed with an error.
    case failed(RelayConnectionError)

    /// Connection was closed (gracefully or by peer).
    case closed
}

/// Errors that can occur during relay connection.
public enum RelayConnectionError: Error, Equatable, Sendable {
    /// Config validation failed.
    case invalidConfig(TunnelRelayConfigError)

    /// WebSocket connection failed.
    case webSocketFailed(String)

    /// SSH handshake failed.
    case sshFailed(String)

    /// Port forwarding channel open failed.
    case channelFailed(String)

    /// Connection timed out.
    case timeout

    /// Connection was rejected (e.g., 401/403).
    case authenticationFailed(String)

    /// Maximum reconnection attempts exhausted.
    case reconnectFailed(attempts: Int)
}

/// Policy for automatic reconnection on connection loss.
public struct ReconnectPolicy: Sendable, Equatable {
    /// Maximum number of reconnection attempts before giving up.
    public let maxAttempts: Int

    /// Initial delay before the first retry (seconds).
    public let initialDelay: TimeInterval

    /// Maximum delay between retries (seconds). Backoff is capped at this value.
    public let maxDelay: TimeInterval

    /// Multiplier applied to the delay after each failed attempt.
    public let backoffMultiplier: Double

    /// Do not attempt to reconnect.
    public static let disabled = ReconnectPolicy(maxAttempts: 0)

    /// Default policy: up to 5 attempts with 1–30s exponential backoff.
    public static let `default` = ReconnectPolicy()

    public init(
        maxAttempts: Int = 5,
        initialDelay: TimeInterval = 1,
        maxDelay: TimeInterval = 30,
        backoffMultiplier: Double = 2.0
    ) {
        self.maxAttempts = maxAttempts
        self.initialDelay = initialDelay
        self.maxDelay = maxDelay
        self.backoffMultiplier = backoffMultiplier
    }

    /// Computes the delay for a given attempt number (0-based).
    func delay(forAttempt attempt: Int) -> TimeInterval {
        let raw = initialDelay * pow(backoffMultiplier, Double(attempt))
        return min(raw, maxDelay)
    }
}

/// Protocol for observing relay connection state changes.
public protocol RelayConnectionDelegate: AnyObject, Sendable {
    /// Called when the connection state changes.
    func relayConnectionStateDidChange(_ state: RelayConnectionState)

    /// Called when data is received from the forwarded port.
    func relayConnectionDidReceiveData(_ data: Data)
}
