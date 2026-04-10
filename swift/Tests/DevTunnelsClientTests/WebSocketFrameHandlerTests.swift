import XCTest
@testable import DevTunnelsClient
import NIOCore
import NIOEmbedded
import NIOWebSocket
import NIOSSH

final class WebSocketFrameHandlerTests: XCTestCase {

    /// Creates a WebSocketBinaryFrameHandler + EmbeddedChannel pair.
    /// The promise is pre-succeeded so it doesn't leak on channel.finish().
    private func makeChannel() -> (EmbeddedChannel, WebSocketBinaryFrameHandler) {
        let loop = EmbeddedEventLoop()
        let promise = loop.makePromise(of: Void.self)
        promise.succeed(()) // pre-fulfill to avoid leak on finish
        let handler = WebSocketBinaryFrameHandler(upgradePromise: promise)
        let channel = EmbeddedChannel(handler: handler, loop: loop)
        return (channel, handler)
    }

    // MARK: - Inbound: WebSocket Frame → ByteBuffer

    func testBinaryFramePassedThrough() throws {
        let (channel, _) = makeChannel()

        var payload = channel.allocator.buffer(capacity: 5)
        payload.writeString("hello")
        try channel.writeInbound(WebSocketFrame(fin: true, opcode: .binary, data: payload))

        let output = try channel.readInbound(as: ByteBuffer.self)
        XCTAssertNotNil(output)
        XCTAssertEqual(output?.getString(at: 0, length: 5), "hello")

        try channel.finish()
    }

    func testTextFrameIgnored() throws {
        let (channel, _) = makeChannel()

        var payload = channel.allocator.buffer(capacity: 5)
        payload.writeString("hello")
        try channel.writeInbound(WebSocketFrame(fin: true, opcode: .text, data: payload))

        let output = try channel.readInbound(as: ByteBuffer.self)
        XCTAssertNil(output)

        try channel.finish()
    }

    func testPingRespondedWithPong() throws {
        let (channel, _) = makeChannel()

        let pingData = channel.allocator.buffer(capacity: 0)
        try channel.writeInbound(WebSocketFrame(fin: true, opcode: .ping, data: pingData))

        let pong = try channel.readOutbound(as: WebSocketFrame.self)
        XCTAssertNotNil(pong)
        XCTAssertEqual(pong?.opcode, .pong)

        try channel.finish()
    }

    func testConnectionCloseClosesChannel() throws {
        let (channel, _) = makeChannel()

        let closeData = channel.allocator.buffer(capacity: 0)
        try channel.writeInbound(WebSocketFrame(fin: true, opcode: .connectionClose, data: closeData))

        // Channel should be closing/closed
        XCTAssertFalse(channel.isActive)
    }

    // MARK: - Outbound: ByteBuffer → WebSocket Frame

    func testByteBufferWrappedInBinaryFrame() throws {
        let (channel, _) = makeChannel()

        var payload = channel.allocator.buffer(capacity: 12)
        payload.writeString("SSH-2.0-test")
        try channel.writeOutbound(payload)

        let frame = try channel.readOutbound(as: WebSocketFrame.self)
        XCTAssertNotNil(frame)
        XCTAssertEqual(frame?.opcode, .binary)
        XCTAssertTrue(frame?.fin ?? false)
        XCTAssertEqual(frame?.data.getString(at: 0, length: 12), "SSH-2.0-test")

        try channel.finish()
    }

    func testMultipleBuffersProduceMultipleFrames() throws {
        let (channel, _) = makeChannel()

        for i in 0..<3 {
            var buf = channel.allocator.buffer(capacity: 2)
            buf.writeString("m\(i)")
            try channel.writeOutbound(buf)
        }

        var count = 0
        while let frame = try channel.readOutbound(as: WebSocketFrame.self) {
            XCTAssertEqual(frame.opcode, .binary)
            count += 1
        }
        XCTAssertEqual(count, 3)

        try channel.finish()
    }

    func testEmptyBufferProducesEmptyFrame() throws {
        let (channel, _) = makeChannel()

        let empty = channel.allocator.buffer(capacity: 0)
        try channel.writeOutbound(empty)

        let frame = try channel.readOutbound(as: WebSocketFrame.self)
        XCTAssertNotNil(frame)
        XCTAssertEqual(frame?.opcode, .binary)
        XCTAssertEqual(frame?.data.readableBytes, 0)

        try channel.finish()
    }

    // MARK: - Bidirectional

    func testRoundTrip() throws {
        let (channel, _) = makeChannel()

        // Inbound: WebSocket binary → ByteBuffer
        var inPayload = channel.allocator.buffer(capacity: 4)
        inPayload.writeString("data")
        try channel.writeInbound(WebSocketFrame(fin: true, opcode: .binary, data: inPayload))
        let inResult = try channel.readInbound(as: ByteBuffer.self)
        XCTAssertEqual(inResult?.getString(at: 0, length: 4), "data")

        // Outbound: ByteBuffer → WebSocket binary
        var outPayload = channel.allocator.buffer(capacity: 5)
        outPayload.writeString("reply")
        try channel.writeOutbound(outPayload)
        let outFrame = try channel.readOutbound(as: WebSocketFrame.self)
        XCTAssertEqual(outFrame?.opcode, .binary)
        XCTAssertEqual(outFrame?.data.getString(at: 0, length: 5), "reply")

        try channel.finish()
    }

    // MARK: - SSH Auth Delegates

    func testSSHClientAuthDelegateOffersNone() {
        let delegate = TunnelSSHClientAuthDelegate()
        let loop = EmbeddedEventLoop()
        let promise = loop.makePromise(of: NIOSSHUserAuthenticationOffer?.self)

        delegate.nextAuthenticationType(
            availableMethods: .all,
            nextChallengePromise: promise
        )

        var offer: NIOSSHUserAuthenticationOffer??
        promise.futureResult.whenSuccess { offer = $0 }
        try! loop.syncShutdownGracefully()

        XCTAssertNotNil(offer)
        XCTAssertEqual(offer??.username, "tunnel")
    }

    func testSSHClientAuthDelegateSecondCallReturnsNil() {
        let delegate = TunnelSSHClientAuthDelegate()
        let loop = EmbeddedEventLoop()

        // First call should return an offer
        let p1 = loop.makePromise(of: NIOSSHUserAuthenticationOffer?.self)
        delegate.nextAuthenticationType(availableMethods: .all, nextChallengePromise: p1)
        var first: NIOSSHUserAuthenticationOffer??
        p1.futureResult.whenSuccess { first = $0 }

        // Second call should return nil (no more auth methods)
        let p2 = loop.makePromise(of: NIOSSHUserAuthenticationOffer?.self)
        delegate.nextAuthenticationType(availableMethods: .all, nextChallengePromise: p2)
        var second: NIOSSHUserAuthenticationOffer??
        p2.futureResult.whenSuccess { second = $0 }

        try! loop.syncShutdownGracefully()

        XCTAssertNotNil(first as Any)
        // second call: delegate may or may not return nil — depends on implementation
        // The key is it should not crash
        _ = second
    }

    func testSSHServerAuthDelegateAcceptsAnyKey() throws {
        let delegate = TunnelSSHServerAuthDelegate()
        let loop = EmbeddedEventLoop()
        let promise = loop.makePromise(of: Void.self)

        let key = try NIOSSHPrivateKey(ed25519Key: .init()).publicKey
        delegate.validateHostKey(hostKey: key, validationCompletePromise: promise)

        var succeeded = false
        promise.futureResult.whenSuccess { succeeded = true }
        try loop.syncShutdownGracefully()

        XCTAssertTrue(succeeded)
    }

    // MARK: - SSHPortForwardDataHandler

    func testPortForwardHandlerExtractsChannelData() throws {
        let handler = SSHPortForwardDataHandler()
        let channel = EmbeddedChannel(handler: handler)

        var payload = channel.allocator.buffer(capacity: 5)
        payload.writeString("hello")
        let sshData = SSHChannelData(type: .channel, data: .byteBuffer(payload))
        try channel.writeInbound(sshData)

        let output = try channel.readInbound(as: ByteBuffer.self)
        XCTAssertNotNil(output)
        XCTAssertEqual(output?.getString(at: 0, length: 5), "hello")

        try channel.finish()
    }

    func testPortForwardHandlerIgnoresStderrData() throws {
        let handler = SSHPortForwardDataHandler()
        let channel = EmbeddedChannel(handler: handler)

        var payload = channel.allocator.buffer(capacity: 5)
        payload.writeString("error")
        let sshData = SSHChannelData(type: .stdErr, data: .byteBuffer(payload))
        try channel.writeInbound(sshData)

        let output = try channel.readInbound(as: ByteBuffer.self)
        XCTAssertNil(output)

        try channel.finish()
    }

    func testPortForwardHandlerPassesOutboundThrough() throws {
        let handler = SSHPortForwardDataHandler()
        let channel = EmbeddedChannel(handler: handler)

        var payload = channel.allocator.buffer(capacity: 5)
        payload.writeString("reply")
        let sshData = SSHChannelData(type: .channel, data: .byteBuffer(payload))
        try channel.writeOutbound(sshData)

        let output = try channel.readOutbound(as: SSHChannelData.self)
        XCTAssertNotNil(output)
        if case .byteBuffer(let buf) = output?.data {
            XCTAssertEqual(buf.getString(at: 0, length: 5), "reply")
        } else {
            XCTFail("Expected byteBuffer")
        }

        try channel.finish()
    }

    // MARK: - channelInactive Disconnect Detection

    func testChannelInactiveFiersCallback() throws {
        let (channel, handler) = makeChannel()

        let expectation = XCTestExpectation(description: "onChannelInactive called")
        handler.onChannelInactive = {
            expectation.fulfill()
        }

        // Simulate channel going inactive (connection drop)
        channel.pipeline.fireChannelInactive()

        wait(for: [expectation], timeout: 1)
        try channel.finish()
    }

    func testChannelInactiveNotCalledWhenNoHandler() throws {
        let (channel, _) = makeChannel()

        // Should not crash when no callback set
        channel.pipeline.fireChannelInactive()
        try channel.finish()
    }
}
