import Foundation
import NIOCore
import NIOPosix
import NIOHTTP1
import NIOWebSocket
import NIOSSH
import NIOTransportServices
import Network

/// A bidirectional stream to a forwarded port through a tunnel relay.
///
/// Wraps the full WebSocket → SSH → port forwarding pipeline.
/// Connection flow:
/// 1. TCP connect to relay host (with TLS for wss://)
/// 2. HTTP → WebSocket upgrade with `tunnel-relay-client` subprotocol
/// 3. SSH handshake over WebSocket (user: "tunnel")
/// 4. Open `forwarded-tcpip` channel for the requested port
/// 5. Bidirectional data streaming through the SSH channel
///
/// Sends periodic WebSocket pings to keep the connection alive.
public final class TunnelRelayStream: @unchecked Sendable {
    private let parentChannel: Channel
    private let sshChildChannel: Channel
    private let group: EventLoopGroup
    private var _isClosed = false
    private var keepaliveTask: Scheduled<Void>?

    /// Callback invoked when the connection drops unexpectedly.
    /// Not called for explicit `close()` calls.
    internal var onDisconnect: (@Sendable () -> Void)?

    init(parentChannel: Channel, sshChildChannel: Channel, group: EventLoopGroup) {
        self.parentChannel = parentChannel
        self.sshChildChannel = sshChildChannel
        self.group = group
    }

    /// Connects to the tunnel relay and establishes SSH port forwarding.
    static func connect(
        config: TunnelRelayConfig,
        onStateChange: @escaping @Sendable (RelayConnectionState) -> Void
    ) async throws -> TunnelRelayStream {
        guard let url = URL(string: config.relayUri),
              let host = url.host,
              let scheme = url.scheme else {
            throw RelayConnectionError.webSocketFailed("Invalid relay URI: \(config.relayUri)")
        }

        let useTLS = scheme == "wss"
        let port = url.port ?? (useTLS ? 443 : 80)

        let group: EventLoopGroup
        #if canImport(Network)
        group = NIOTSEventLoopGroup()
        #else
        group = MultiThreadedEventLoopGroup(numberOfThreads: 1)
        #endif

        do {
            onStateChange(.connectingWebSocket)

            // Step 1: TCP connect + WebSocket upgrade
            let upgradePromise = group.next().makePromise(of: Void.self)
            let wsFrameHandler = WebSocketBinaryFrameHandler(upgradePromise: upgradePromise)
            let upgradeHandler = WebSocketUpgradeHandler(
                config: config,
                wsFrameHandler: wsFrameHandler
            )

            let channel: Channel
            #if canImport(Network)
            let tsBootstrap = NIOTSConnectionBootstrap(group: group)
                .channelInitializer { ch in
                    ch.pipeline.addHandler(upgradeHandler)
                }
            if useTLS {
                channel = try await tsBootstrap
                    .tlsOptions(NWProtocolTLS.Options())
                    .connect(host: host, port: port)
                    .get()
            } else {
                channel = try await tsBootstrap
                    .connect(host: host, port: port)
                    .get()
            }
            #else
            let bootstrap = ClientBootstrap(group: group)
                .channelOption(.socketOption(.so_reuseaddr), value: 1)
                .channelInitializer { ch in
                    ch.pipeline.addHandler(upgradeHandler)
                }
            channel = try await bootstrap.connect(host: host, port: port).get()
            #endif

            // Wait for WebSocket upgrade to complete
            try await upgradePromise.futureResult.get()

            onStateChange(.connectingSSH)

            // Step 2: SSH handshake over WebSocket
            let sshHandler = try await addSSHHandlers(to: channel)

            onStateChange(.openingChannel)

            // Step 3: Open port forwarding channel
            let sshChildChannel = try await openPortForwardChannel(
                sshHandler: sshHandler,
                on: channel,
                port: config.port
            )

            let stream = TunnelRelayStream(
                parentChannel: channel,
                sshChildChannel: sshChildChannel,
                group: group
            )

            // Start periodic WebSocket keepalive pings
            if config.keepaliveInterval > 0 {
                stream.startKeepalive(interval: config.keepaliveInterval)
            }

            // Wire disconnect detection: WebSocket channel going inactive
            // triggers the stream's onDisconnect callback.
            wsFrameHandler.onChannelInactive = { [weak stream] in
                guard let stream, !stream._isClosed else { return }
                stream.onDisconnect?()
            }

            return stream
        } catch {
            try? await group.shutdownGracefully()
            if let relayError = error as? RelayConnectionError {
                throw relayError
            }
            throw RelayConnectionError.webSocketFailed(error.localizedDescription)
        }
    }

    /// Adds NIO SSH client handlers to the channel pipeline.
    @discardableResult
    private static func addSSHHandlers(to channel: Channel) async throws -> NIOSSHHandler {
        let sshHandler = NIOSSHHandler(
            role: .client(
                .init(
                    userAuthDelegate: TunnelSSHClientAuthDelegate(),
                    serverAuthDelegate: TunnelSSHServerAuthDelegate()
                )
            ),
            allocator: channel.allocator,
            inboundChildChannelInitializer: nil
        )

        try await channel.pipeline.addHandler(sshHandler).get()
        return sshHandler
    }

    /// Opens a `forwarded-tcpip` SSH channel for the given port.
    private static func openPortForwardChannel(
        sshHandler: NIOSSHHandler,
        on channel: Channel,
        port: UInt16
    ) async throws -> Channel {
        let channelType = SSHChannelType.forwardedTCPIP(
            .init(
                listeningHost: "127.0.0.1",
                listeningPort: Int(port),
                originatorAddress: try .init(ipAddress: "127.0.0.1", port: 0)
            )
        )

        let childChannelPromise = channel.eventLoop.makePromise(of: Channel.self)

        channel.eventLoop.execute {
            sshHandler.createChannel(childChannelPromise, channelType: channelType) { childChannel, _ in
                childChannel.pipeline.addHandler(SSHPortForwardDataHandler())
            }
        }

        return try await childChannelPromise.futureResult.get()
    }

    /// Whether the underlying channels are still active.
    public var isActive: Bool {
        !_isClosed && sshChildChannel.isActive
    }

    /// Starts periodic WebSocket ping frames to keep the connection alive.
    func startKeepalive(interval: TimeInterval) {
        let eventLoop = parentChannel.eventLoop
        func schedulePing() {
            guard !_isClosed, parentChannel.isActive else { return }
            keepaliveTask = eventLoop.scheduleTask(in: .seconds(Int64(interval))) { [weak self] in
                guard let self, !self._isClosed, self.parentChannel.isActive else { return }
                let emptyBuffer = self.parentChannel.allocator.buffer(capacity: 0)
                let ping = WebSocketFrame(fin: true, opcode: .ping, data: emptyBuffer)
                self.parentChannel.writeAndFlush(ping, promise: nil)
                schedulePing()
            }
        }
        schedulePing()
    }

    /// Sends data through the forwarded port.
    public func send(_ data: Data) async throws {
        guard sshChildChannel.isActive else {
            throw RelayConnectionError.channelFailed("Not connected")
        }
        var buffer = sshChildChannel.allocator.buffer(capacity: data.count)
        buffer.writeBytes(data)
        let sshData = SSHChannelData(type: .channel, data: .byteBuffer(buffer))
        try await sshChildChannel.writeAndFlush(sshData)
    }

    /// Closes the relay connection.
    public func close() async throws {
        guard !_isClosed else { return }
        _isClosed = true
        keepaliveTask?.cancel()
        keepaliveTask = nil
        try? await sshChildChannel.close()
        try? await parentChannel.close()
        try await group.shutdownGracefully()
    }

    deinit {
        try? group.syncShutdownGracefully()
    }
}

// MARK: - SSH Port Forward Data Handler

/// Handles data on the SSH child channel (port forwarding).
/// Receives `SSHChannelData` and passes raw bytes upstream.
final class SSHPortForwardDataHandler: ChannelDuplexHandler {
    typealias InboundIn = SSHChannelData
    typealias InboundOut = ByteBuffer
    typealias OutboundIn = SSHChannelData
    typealias OutboundOut = SSHChannelData

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        let channelData = unwrapInboundIn(data)
        guard case .channel = channelData.type,
              case .byteBuffer(let buffer) = channelData.data else {
            return
        }
        context.fireChannelRead(wrapInboundOut(buffer))
    }

    func write(context: ChannelHandlerContext, data: NIOAny, promise: EventLoopPromise<Void>?) {
        let channelData = unwrapOutboundIn(data)
        context.write(wrapOutboundOut(channelData), promise: promise)
    }
}

// MARK: - SSH Auth Delegates

/// SSH client auth delegate that authenticates as "tunnel" user with no password.
/// The tunnel access token (sent via WebSocket Authorization header) provides auth.
final class TunnelSSHClientAuthDelegate: NIOSSHClientUserAuthenticationDelegate {
    func nextAuthenticationType(
        availableMethods: NIOSSHAvailableUserAuthenticationMethods,
        nextChallengePromise: EventLoopPromise<NIOSSHUserAuthenticationOffer?>
    ) {
        // Try "none" authentication first — the tunnel relay trusts the WebSocket token
        nextChallengePromise.succeed(
            NIOSSHUserAuthenticationOffer(
                username: TunnelRelayConstants.sshUser,
                serviceName: "",
                offer: .none
            )
        )
    }
}

/// SSH server auth delegate that accepts any host key.
/// The WebSocket TLS + tunnel access token provide sufficient authentication.
final class TunnelSSHServerAuthDelegate: NIOSSHClientServerAuthenticationDelegate {
    func validateHostKey(
        hostKey: NIOSSHPublicKey,
        validationCompletePromise: EventLoopPromise<Void>
    ) {
        // Accept any host key — same as Go SDK's InsecureIgnoreHostKey
        validationCompletePromise.succeed(())
    }
}

// MARK: - WebSocket Upgrade Handler

/// Handles the HTTP → WebSocket upgrade handshake.
/// After successful upgrade, replaces itself with WebSocket frame handlers.
final class WebSocketUpgradeHandler: ChannelInboundHandler, RemovableChannelHandler {
    typealias InboundIn = ByteBuffer

    private let config: TunnelRelayConfig
    private let wsFrameHandler: WebSocketBinaryFrameHandler

    init(config: TunnelRelayConfig, wsFrameHandler: WebSocketBinaryFrameHandler) {
        self.config = config
        self.wsFrameHandler = wsFrameHandler
    }

    func channelActive(context: ChannelHandlerContext) {
        sendUpgradeRequest(context: context)
    }

    private func sendUpgradeRequest(context: ChannelHandlerContext) {
        guard let url = URL(string: config.relayUri) else { return }
        let path = url.path.isEmpty ? "/" : url.path
        let host = url.host ?? ""

        // Generate random WebSocket key
        var keyBytes = [UInt8](repeating: 0, count: 16)
        _ = SecRandomCopyBytes(kSecRandomDefault, keyBytes.count, &keyBytes)
        let key = Data(keyBytes).base64EncodedString()

        var buffer = context.channel.allocator.buffer(capacity: 512)
        buffer.writeString("GET \(path) HTTP/1.1\r\n")
        buffer.writeString("Host: \(host)\r\n")
        buffer.writeString("Upgrade: websocket\r\n")
        buffer.writeString("Connection: Upgrade\r\n")
        buffer.writeString("Sec-WebSocket-Key: \(key)\r\n")
        buffer.writeString("Sec-WebSocket-Version: 13\r\n")
        buffer.writeString("Sec-WebSocket-Protocol: \(config.subprotocol)\r\n")
        buffer.writeString("Authorization: \(config.authorizationHeader)\r\n")
        buffer.writeString("\r\n")

        context.writeAndFlush(NIOAny(buffer), promise: nil)
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        var buffer = unwrapInboundIn(data)
        guard let response = buffer.readString(length: buffer.readableBytes) else { return }

        if response.contains("101") {
            // Upgrade success — swap to WebSocket frame handlers
            _ = context.pipeline.addHandlers([
                ByteToMessageHandler(WebSocketFrameDecoder()),
                WebSocketFrameEncoder(),
                wsFrameHandler,
            ]).flatMap {
                context.pipeline.removeHandler(self)
            }
            wsFrameHandler.upgradePromise.succeed(())
        } else if response.contains("401") || response.contains("403") {
            wsFrameHandler.upgradePromise.fail(
                RelayConnectionError.authenticationFailed("Relay returned auth error")
            )
        } else {
            wsFrameHandler.upgradePromise.fail(
                RelayConnectionError.webSocketFailed("WebSocket upgrade failed")
            )
        }
    }

    func errorCaught(context: ChannelHandlerContext, error: Error) {
        wsFrameHandler.upgradePromise.fail(
            RelayConnectionError.webSocketFailed(error.localizedDescription)
        )
        context.close(promise: nil)
    }
}

// MARK: - WebSocket Binary Frame Handler

/// Converts between WebSocket binary frames and raw ByteBuffers.
/// Sits between the WebSocket frame codec and the SSH handler.
final class WebSocketBinaryFrameHandler: ChannelDuplexHandler {
    typealias InboundIn = WebSocketFrame
    typealias InboundOut = ByteBuffer
    typealias OutboundIn = ByteBuffer
    typealias OutboundOut = WebSocketFrame

    let upgradePromise: EventLoopPromise<Void>

    /// Called when the channel goes inactive (connection lost).
    var onChannelInactive: (@Sendable () -> Void)?

    init(upgradePromise: EventLoopPromise<Void>) {
        self.upgradePromise = upgradePromise
    }

    func channelRead(context: ChannelHandlerContext, data: NIOAny) {
        let frame = unwrapInboundIn(data)
        switch frame.opcode {
        case .binary:
            let data = frame.unmaskedData
            context.fireChannelRead(wrapInboundOut(data))
        case .connectionClose:
            context.close(promise: nil)
        case .ping:
            let pongData = context.channel.allocator.buffer(capacity: 0)
            let pong = WebSocketFrame(fin: true, opcode: .pong, data: pongData)
            context.writeAndFlush(wrapOutboundOut(pong), promise: nil)
        default:
            break
        }
    }

    func channelInactive(context: ChannelHandlerContext) {
        onChannelInactive?()
        context.fireChannelInactive()
    }

    func write(context: ChannelHandlerContext, data: NIOAny, promise: EventLoopPromise<Void>?) {
        let buffer = unwrapOutboundIn(data)
        let frame = WebSocketFrame(fin: true, opcode: .binary, data: buffer)
        context.write(wrapOutboundOut(frame), promise: promise)
    }
}

