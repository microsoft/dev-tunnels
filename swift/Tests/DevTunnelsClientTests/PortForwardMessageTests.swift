import XCTest
@testable import DevTunnelsClient
import Foundation

final class PortForwardMessageTests: XCTestCase {

    // MARK: - PortForwardChannelOpen

    func testChannelOpenConstants() {
        XCTAssertEqual(PortForwardChannelOpen.channelType, "forwarded-tcpip")
    }

    func testChannelOpenInit() {
        let msg = PortForwardChannelOpen(port: 8080)
        XCTAssertEqual(msg.host, "127.0.0.1")
        XCTAssertEqual(msg.port, 8080)
        XCTAssertEqual(msg.originatorIPAddress, "")
        XCTAssertEqual(msg.originatorPort, 0)
    }

    func testChannelOpenCustomInit() {
        let msg = PortForwardChannelOpen(
            host: "10.0.0.1",
            port: 3000,
            originatorIPAddress: "192.168.1.1",
            originatorPort: 54321
        )
        XCTAssertEqual(msg.host, "10.0.0.1")
        XCTAssertEqual(msg.port, 3000)
        XCTAssertEqual(msg.originatorIPAddress, "192.168.1.1")
        XCTAssertEqual(msg.originatorPort, 54321)
    }

    func testChannelOpenMarshalRoundTrip() {
        let original = PortForwardChannelOpen(port: 8080)
        let data = original.marshal()
        let decoded = PortForwardChannelOpen.unmarshal(from: data)

        XCTAssertNotNil(decoded)
        XCTAssertEqual(decoded, original)
    }

    func testChannelOpenMarshalCustomRoundTrip() {
        let original = PortForwardChannelOpen(
            host: "example.com",
            port: 443,
            originatorIPAddress: "10.0.0.5",
            originatorPort: 12345
        )
        let data = original.marshal()
        let decoded = PortForwardChannelOpen.unmarshal(from: data)

        XCTAssertEqual(decoded, original)
    }

    func testChannelOpenMarshalBinaryFormat() {
        let msg = PortForwardChannelOpen(host: "AB", port: 1, originatorIPAddress: "C", originatorPort: 2)
        let data = msg.marshal()

        // Expected binary layout:
        // [0..3]   = uint32(2) big-endian   → host length
        // [4..5]   = "AB"                   → host
        // [6..9]   = uint32(1) big-endian   → port
        // [10..13] = uint32(1) big-endian   → originator IP length
        // [14]     = "C"                    → originator IP
        // [15..18] = uint32(2) big-endian   → originator port

        // Total: 4 + 2 + 4 + 4 + 1 + 4 = 19 bytes
        XCTAssertEqual(data.count, 19)

        // Verify host length (big-endian 2)
        XCTAssertEqual(data[0], 0)
        XCTAssertEqual(data[1], 0)
        XCTAssertEqual(data[2], 0)
        XCTAssertEqual(data[3], 2)

        // Verify host
        XCTAssertEqual(data[4], UInt8(ascii: "A"))
        XCTAssertEqual(data[5], UInt8(ascii: "B"))

        // Verify port (big-endian 1)
        XCTAssertEqual(data[6], 0)
        XCTAssertEqual(data[7], 0)
        XCTAssertEqual(data[8], 0)
        XCTAssertEqual(data[9], 1)
    }

    func testChannelOpenEmptyHost() {
        let msg = PortForwardChannelOpen(host: "", port: 80)
        let data = msg.marshal()
        let decoded = PortForwardChannelOpen.unmarshal(from: data)
        XCTAssertEqual(decoded?.host, "")
        XCTAssertEqual(decoded?.port, 80)
    }

    func testChannelOpenLargePort() {
        let msg = PortForwardChannelOpen(port: 65535)
        let data = msg.marshal()
        let decoded = PortForwardChannelOpen.unmarshal(from: data)
        XCTAssertEqual(decoded?.port, 65535)
    }

    func testChannelOpenUnmarshalTruncatedData() {
        // Only 3 bytes — not enough for even the first uint32
        let data = Data([0, 0, 1])
        let decoded = PortForwardChannelOpen.unmarshal(from: data)
        XCTAssertNil(decoded)
    }

    /// Regression: a hostile peer that declares a string length > Int.max
    /// (or just absurdly large) must not crash decoding. The reader returns
    /// nil so the surrounding `unmarshal` rejects the message.
    func testChannelOpenUnmarshalRejectsHugeStringLength() {
        // Length prefix = UInt32.max followed by no data
        var data = Data()
        data.append(contentsOf: [0xFF, 0xFF, 0xFF, 0xFF])
        let decoded = PortForwardChannelOpen.unmarshal(from: data)
        XCTAssertNil(decoded)
    }

    func testChannelOpenUnmarshalEmptyData() {
        let decoded = PortForwardChannelOpen.unmarshal(from: Data())
        XCTAssertNil(decoded)
    }

    func testChannelOpenEquality() {
        let a = PortForwardChannelOpen(port: 8080)
        let b = PortForwardChannelOpen(port: 8080)
        let c = PortForwardChannelOpen(port: 9090)
        XCTAssertEqual(a, b)
        XCTAssertNotEqual(a, c)
    }

    // MARK: - PortForwardRequest

    func testRequestConstants() {
        XCTAssertEqual(PortForwardRequest.requestType, "tcpip-forward")
    }

    func testRequestInit() {
        let req = PortForwardRequest(port: 3000)
        XCTAssertEqual(req.address, "127.0.0.1")
        XCTAssertEqual(req.port, 3000)
    }

    func testRequestMarshalRoundTrip() {
        let original = PortForwardRequest(address: "0.0.0.0", port: 443)
        let data = original.marshal()
        let decoded = PortForwardRequest.unmarshal(from: data)
        XCTAssertEqual(decoded, original)
    }

    func testRequestUnmarshalTruncated() {
        let data = Data([0, 0, 0, 5]) // length but no string bytes
        let decoded = PortForwardRequest.unmarshal(from: data)
        XCTAssertNil(decoded)
    }

    func testRequestEquality() {
        let a = PortForwardRequest(port: 80)
        let b = PortForwardRequest(port: 80)
        let c = PortForwardRequest(address: "0.0.0.0", port: 80)
        XCTAssertEqual(a, b)
        XCTAssertNotEqual(a, c)
    }

    // MARK: - PortForwardSuccess

    func testSuccessInit() {
        let success = PortForwardSuccess(port: 8080)
        XCTAssertEqual(success.port, 8080)
    }

    func testSuccessMarshalRoundTrip() {
        let original = PortForwardSuccess(port: 12345)
        let data = original.marshal()

        // Should be exactly 4 bytes (one uint32)
        XCTAssertEqual(data.count, 4)

        let decoded = PortForwardSuccess.unmarshal(from: data)
        XCTAssertEqual(decoded, original)
    }

    func testSuccessUnmarshalTruncated() {
        let data = Data([0, 0])
        let decoded = PortForwardSuccess.unmarshal(from: data)
        XCTAssertNil(decoded)
    }

    func testSuccessEquality() {
        XCTAssertEqual(PortForwardSuccess(port: 80), PortForwardSuccess(port: 80))
        XCTAssertNotEqual(PortForwardSuccess(port: 80), PortForwardSuccess(port: 443))
    }

    // MARK: - Cross-compatibility with Go SDK format

    func testGoSDKCompatibleChannelOpen() {
        // The Go SDK creates: NewPortForwardChannel(senderChannel, "127.0.0.1", uint32(port), "", 0)
        // We should produce identical binary output for the payload portion
        let msg = PortForwardChannelOpen(
            host: "127.0.0.1",
            port: 8080,
            originatorIPAddress: "",
            originatorPort: 0
        )
        let data = msg.marshal()

        // Verify we can round-trip
        let decoded = PortForwardChannelOpen.unmarshal(from: data)
        XCTAssertEqual(decoded, msg)

        // Verify the host is "127.0.0.1" (9 bytes) → length prefix = 9
        XCTAssertEqual(data[0], 0)
        XCTAssertEqual(data[1], 0)
        XCTAssertEqual(data[2], 0)
        XCTAssertEqual(data[3], 9) // length of "127.0.0.1"

        // Verify originator IP is empty string → length prefix = 0
        let portEndOffset = 4 + 9 + 4 // host_len(4) + host(9) + port(4)
        XCTAssertEqual(data[portEndOffset], 0)
        XCTAssertEqual(data[portEndOffset + 1], 0)
        XCTAssertEqual(data[portEndOffset + 2], 0)
        XCTAssertEqual(data[portEndOffset + 3], 0) // empty string length
    }
}
