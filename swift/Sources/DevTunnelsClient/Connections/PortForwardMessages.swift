import Foundation
import NIOCore

/// SSH port forwarding message types for the tunnel relay protocol.
///
/// Port forwarding in the Dev Tunnels relay uses SSH `forwarded-tcpip` channels.
/// The client opens a channel to connect to a specific port on the tunnel host.

// MARK: - Port Forward Channel Open

/// Payload for opening a `forwarded-tcpip` SSH channel.
///
/// Binary format (SSH wire protocol, big-endian):
///   - string: host (e.g. "127.0.0.1")
///   - uint32: port
///   - string: originator IP address (empty string for tunnel client)
///   - uint32: originator port (0 for tunnel client)
public struct PortForwardChannelOpen: Equatable, Sendable {
    /// The SSH channel type used for port forwarding.
    public static let channelType = "forwarded-tcpip"

    /// The host to connect to on the tunnel server side.
    public let host: String

    /// The port to forward.
    public let port: UInt32

    /// The IP address of the originator (client).
    public let originatorIPAddress: String

    /// The port on the originator (client).
    public let originatorPort: UInt32

    public init(host: String = "127.0.0.1", port: UInt32, originatorIPAddress: String = "", originatorPort: UInt32 = 0) {
        self.host = host
        self.port = port
        self.originatorIPAddress = originatorIPAddress
        self.originatorPort = originatorPort
    }

    /// Serializes this message to SSH wire format.
    public func marshal() -> Data {
        var data = Data()
        writeString(&data, host)
        writeUInt32(&data, port)
        writeString(&data, originatorIPAddress)
        writeUInt32(&data, originatorPort)
        return data
    }

    /// Deserializes from SSH wire format.
    public static func unmarshal(from data: Data) -> PortForwardChannelOpen? {
        var offset = 0

        guard let host = readString(from: data, offset: &offset) else { return nil }
        guard let port = readUInt32(from: data, offset: &offset) else { return nil }
        guard let originatorIP = readString(from: data, offset: &offset) else { return nil }
        guard let originatorPort = readUInt32(from: data, offset: &offset) else { return nil }

        return PortForwardChannelOpen(
            host: host,
            port: port,
            originatorIPAddress: originatorIP,
            originatorPort: originatorPort
        )
    }
}

// MARK: - Port Forward Request (tcpip-forward)

/// Global request payload for `tcpip-forward`.
///
/// The server sends this to notify the client that a port is available for forwarding.
///
/// Binary format:
///   - string: address to bind (e.g. "127.0.0.1")
///   - uint32: port number
public struct PortForwardRequest: Equatable, Sendable {
    /// The SSH global request type.
    public static let requestType = "tcpip-forward"

    /// The address to bind.
    public let address: String

    /// The port number.
    public let port: UInt32

    public init(address: String = "127.0.0.1", port: UInt32) {
        self.address = address
        self.port = port
    }

    /// Serializes this message to SSH wire format.
    public func marshal() -> Data {
        var data = Data()
        writeString(&data, address)
        writeUInt32(&data, port)
        return data
    }

    /// Deserializes from SSH wire format.
    public static func unmarshal(from data: Data) -> PortForwardRequest? {
        var offset = 0
        guard let address = readString(from: data, offset: &offset) else { return nil }
        guard let port = readUInt32(from: data, offset: &offset) else { return nil }
        return PortForwardRequest(address: address, port: port)
    }
}

// MARK: - Port Forward Success Response

/// Response payload for a successful `tcpip-forward` request.
///
/// Binary format:
///   - uint32: the port that was actually bound
public struct PortForwardSuccess: Equatable, Sendable {
    /// The port that was bound.
    public let port: UInt32

    public init(port: UInt32) {
        self.port = port
    }

    public func marshal() -> Data {
        var data = Data()
        writeUInt32(&data, port)
        return data
    }

    public static func unmarshal(from data: Data) -> PortForwardSuccess? {
        var offset = 0
        guard let port = readUInt32(from: data, offset: &offset) else { return nil }
        return PortForwardSuccess(port: port)
    }
}

// MARK: - SSH Wire Format Helpers

/// Writes an SSH string (uint32 length + UTF-8 bytes) to data.
private func writeString(_ data: inout Data, _ string: String) {
    let bytes = Array(string.utf8)
    writeUInt32(&data, UInt32(bytes.count))
    data.append(contentsOf: bytes)
}

/// Writes a big-endian uint32 to data.
private func writeUInt32(_ data: inout Data, _ value: UInt32) {
    var bigEndian = value.bigEndian
    data.append(Data(bytes: &bigEndian, count: 4))
}

/// Reads an SSH string from data at the given offset.
private func readString(from data: Data, offset: inout Int) -> String? {
    guard let length = readUInt32(from: data, offset: &offset) else { return nil }
    // Defensive: guard against UInt32 → Int overflow on (hypothetical) 32-bit
    // platforms where Int.max < UInt32.max. On 64-bit this always succeeds.
    guard let len = Int(exactly: length) else { return nil }
    guard offset + len <= data.count else { return nil }
    let string = String(data: data[offset..<offset + len], encoding: .utf8)
    offset += len
    return string
}

/// Reads a big-endian uint32 from data at the given offset.
private func readUInt32(from data: Data, offset: inout Int) -> UInt32? {
    guard offset + 4 <= data.count else { return nil }
    let b0 = UInt32(data[offset])
    let b1 = UInt32(data[offset + 1])
    let b2 = UInt32(data[offset + 2])
    let b3 = UInt32(data[offset + 3])
    offset += 4
    return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3
}
