import Foundation
import NIOCore

/// A client that connects to a Dev Tunnel via the relay service.
///
/// Connection flow:
/// 1. WebSocket → relay URI with `tunnel-relay-client` subprotocol
/// 2. SSH handshake over WebSocket (user: "tunnel", no password)
/// 3. Open `forwarded-tcpip` channel for the requested port
/// 4. Bidirectional data streaming through the SSH channel
///
/// Supports automatic reconnection when the connection drops unexpectedly.
/// Use ``connectWithReconnect(policy:)`` to get an ``AsyncStream`` of streams
/// that yields a new stream on each (re)connection.
///
/// Usage:
/// ```swift
/// let client = TunnelRelayClient(config: config)
///
/// // Simple one-shot connection:
/// let stream = try await client.connect()
///
/// // Auto-reconnecting connection:
/// for await stream in client.connectWithReconnect() {
///     // Use stream; loop yields a new one after reconnect
/// }
/// ```
public final class TunnelRelayClient: Sendable {
    private let config: TunnelRelayConfig
    private let _state: LockedValue<RelayConnectionState>
    private let _stream: LockedValue<TunnelRelayStream?>

    private let _onStateChange: LockedValue<(@Sendable (RelayConnectionState) -> Void)?>

    /// Callback invoked on every state change (connected, disconnected, reconnecting, etc.).
    /// Set this before calling ``connect()`` or ``connectWithReconnect(policy:)``.
    public var onStateChangeHandler: (@Sendable (RelayConnectionState) -> Void)? {
        get { _onStateChange.withLockedValue { $0 } }
        set { _onStateChange.withLockedValue { $0 = newValue } }
    }

    /// Current connection state.
    public var state: RelayConnectionState {
        _state.withLockedValue { $0 }
    }

    /// Creates a new relay client with the given configuration.
    public init(config: TunnelRelayConfig) {
        self.config = config
        self._state = LockedValue(.disconnected)
        self._stream = LockedValue(nil)
        self._onStateChange = LockedValue(nil)
    }

    /// Creates a relay client from a tunnel object.
    ///
    /// Extracts the relay URI and connect token from the tunnel's endpoints and access tokens.
    ///
    /// - Parameters:
    ///   - tunnel: Tunnel with endpoints and access tokens.
    ///   - port: Port to forward.
    /// - Returns: Configured client, or nil if tunnel lacks relay endpoint or connect token.
    public static func fromTunnel(_ tunnel: Tunnel, port: UInt16) -> TunnelRelayClient? {
        guard let relayUri = TunnelConnection.clientRelayURI(from: tunnel),
              let token = TunnelConnection.connectToken(from: tunnel) else {
            return nil
        }
        let config = TunnelRelayConfig(
            relayUri: relayUri,
            accessToken: token,
            port: port
        )
        return TunnelRelayClient(config: config)
    }

    /// Validates the configuration without connecting.
    public func validateConfig() -> TunnelRelayConfigError? {
        config.validate()
    }

    /// Connects to the tunnel relay and opens a port forwarding channel.
    ///
    /// - Returns: A bidirectional stream for the forwarded port.
    /// - Throws: `RelayConnectionError` if connection fails at any stage.
    public func connect() async throws -> TunnelRelayStream {
        if let error = config.validate() {
            transition(to: .failed(.invalidConfig(error)))
            throw RelayConnectionError.invalidConfig(error)
        }

        transition(to: .connectingWebSocket)

        let stream = try await TunnelRelayStream.connect(config: config) { [weak self] newState in
            self?.transition(to: newState)
        }

        _stream.withLockedValue { $0 = stream }
        transition(to: .connected)
        return stream
    }

    /// Connects with automatic reconnection on unexpected disconnects.
    ///
    /// Returns an `AsyncStream` that yields a new ``TunnelRelayStream`` on each
    /// successful connection (initial and subsequent reconnects). The stream finishes
    /// when reconnection is exhausted or ``disconnect()`` is called.
    ///
    /// - Parameter policy: Reconnection policy (defaults to ``ReconnectPolicy/default``).
    /// - Returns: An async stream of relay streams, one per (re)connection.
    public func connectWithReconnect(
        policy: ReconnectPolicy = .default
    ) -> AsyncStream<TunnelRelayStream> {
        AsyncStream { continuation in
            let task = Task { [weak self] in
                guard let self else {
                    continuation.finish()
                    return
                }

                // Initial connection
                do {
                    let stream = try await self.connect()
                    self.wireDisconnect(stream: stream)
                    continuation.yield(stream)
                } catch {
                    self.transition(to: .failed(error as? RelayConnectionError ?? .webSocketFailed(error.localizedDescription)))
                    continuation.finish()
                    return
                }

                // Reconnection loop
                var attempt = 0
                while !Task.isCancelled {
                    // Wait for disconnection
                    await self.waitForDisconnect()

                    guard !Task.isCancelled else { break }

                    // Check if this was an intentional close
                    let currentState = self.state
                    if currentState == .closed {
                        break
                    }

                    // Retry with backoff
                    while attempt < policy.maxAttempts && !Task.isCancelled {
                        attempt += 1
                        self.transition(to: .reconnecting(attempt: attempt))

                        let delay = policy.delay(forAttempt: attempt - 1)
                        try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000))

                        guard !Task.isCancelled else { break }

                        do {
                            // Close old resources before reconnecting
                            let oldStream = self._stream.withLockedValue { s -> TunnelRelayStream? in
                                let old = s
                                s = nil
                                return old
                            }
                            if let oldStream {
                                try? await oldStream.close()
                            }

                            let stream = try await self.connect()
                            self.wireDisconnect(stream: stream)
                            continuation.yield(stream)
                            attempt = 0  // Reset on success
                            break
                        } catch {
                            if attempt >= policy.maxAttempts {
                                self.transition(to: .failed(.reconnectFailed(attempts: attempt)))
                                continuation.finish()
                                return
                            }
                            // Continue to next attempt
                        }
                    }

                    if attempt >= policy.maxAttempts {
                        break
                    }
                }
                continuation.finish()
            }

            continuation.onTermination = { _ in
                task.cancel()
            }
        }
    }

    /// Disconnects from the relay, closing all channels.
    public func disconnect() {
        transition(to: .closed)
        let stream = _stream.withLockedValue { s -> TunnelRelayStream? in
            let old = s
            s = nil
            return old
        }
        if let stream {
            Task {
                try? await stream.close()
            }
        }
        // Signal the reconnect loop to stop
        _disconnectContinuation.withLockedValue { c in
            c?.resume()
            c = nil
        }
    }

    // MARK: - Internal

    private let _disconnectContinuation: LockedValue<CheckedContinuation<Void, Never>?> = LockedValue(nil)

    private func transition(to newState: RelayConnectionState) {
        _state.withLockedValue { $0 = newState }
        _onStateChange.withLockedValue { $0?(newState) }
    }

    /// Wires the stream's disconnect callback to resume the reconnect loop.
    private func wireDisconnect(stream: TunnelRelayStream) {
        stream.onDisconnect = { [weak self] in
            guard let self else { return }
            self.transition(to: .disconnected)
            self._disconnectContinuation.withLockedValue { c in
                c?.resume()
                c = nil
            }
        }
    }

    /// Suspends until the current stream disconnects.
    private func waitForDisconnect() async {
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            // If already disconnected, resume immediately
            let currentState = self.state
            if currentState == .disconnected || currentState == .closed {
                continuation.resume()
                return
            }
            self._disconnectContinuation.withLockedValue { $0 = continuation }
        }
    }
}

/// A thread-safe locked value container.
///
/// NIOCore provides `NIOLockedValueBox` but we use a simple version
/// for clarity and to avoid tight NIO coupling in the public API.
internal final class LockedValue<Value>: @unchecked Sendable {
    private var value: Value
    private let lock = NSLock()

    init(_ value: Value) {
        self.value = value
    }

    func withLockedValue<T>(_ body: (inout Value) -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return body(&value)
    }
}
