import XCTest
@testable import DevTunnelsClient

final class ContractTests: XCTestCase {

    // MARK: - Tunnel

    func testTunnelDefaultInit() {
        let tunnel = Tunnel()
        XCTAssertNil(tunnel.clusterId)
        XCTAssertNil(tunnel.tunnelId)
        XCTAssertNil(tunnel.name)
        XCTAssertNil(tunnel.ports)
        XCTAssertNil(tunnel.endpoints)
        XCTAssertNil(tunnel.status)
    }

    func testTunnelFullInit() {
        let tunnel = Tunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            name: "my-tunnel",
            description: "Test tunnel",
            labels: ["dev", "test"],
            accessTokens: ["connect": "jwt-token"],
            ports: [TunnelPort(portNumber: 8080)]
        )
        XCTAssertEqual(tunnel.clusterId, "usw2")
        XCTAssertEqual(tunnel.tunnelId, "abc123")
        XCTAssertEqual(tunnel.name, "my-tunnel")
        XCTAssertEqual(tunnel.description, "Test tunnel")
        XCTAssertEqual(tunnel.labels, ["dev", "test"])
        XCTAssertEqual(tunnel.accessTokens?["connect"], "jwt-token")
        XCTAssertEqual(tunnel.ports?.count, 1)
        XCTAssertEqual(tunnel.ports?[0].portNumber, 8080)
    }

    func testTunnelEquality() {
        let a = Tunnel(clusterId: "usw2", tunnelId: "abc")
        let b = Tunnel(clusterId: "usw2", tunnelId: "abc")
        let c = Tunnel(clusterId: "usw2", tunnelId: "xyz")
        XCTAssertEqual(a, b)
        XCTAssertNotEqual(a, c)
    }

    func testTunnelCodableRoundTrip() throws {
        let original = Tunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            name: "my-tunnel",
            accessTokens: ["connect": "token123"],
            ports: [TunnelPort(portNumber: 8080, name: "web")]
        )
        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(Tunnel.self, from: data)
        XCTAssertEqual(original, decoded)
    }

    func testTunnelDecodesFromAPIResponse() throws {
        let json = """
        {
            "clusterId": "usw2",
            "tunnelId": "abc123",
            "name": "my-tunnel",
            "description": "A test tunnel",
            "labels": ["dev"],
            "accessTokens": {
                "connect": "eyJ..."
            },
            "status": {
                "hostConnectionCount": {
                    "current": 1,
                    "limit": 5
                }
            },
            "endpoints": [
                {
                    "connectionMode": "TunnelRelay",
                    "hostId": "host-1",
                    "clientRelayUri": "wss://usw2-data.rel.tunnels.api.visualstudio.com/..."
                }
            ],
            "ports": [
                {
                    "portNumber": 8080,
                    "name": "web",
                    "protocol": "http"
                },
                {
                    "portNumber": 31546
                }
            ]
        }
        """
        let tunnel = try JSONDecoder().decode(Tunnel.self, from: Data(json.utf8))
        XCTAssertEqual(tunnel.clusterId, "usw2")
        XCTAssertEqual(tunnel.tunnelId, "abc123")
        XCTAssertEqual(tunnel.name, "my-tunnel")
        XCTAssertEqual(tunnel.labels, ["dev"])
        XCTAssertEqual(tunnel.accessTokens?["connect"], "eyJ...")
        XCTAssertEqual(tunnel.status?.hostConnectionCount?.current, 1)
        XCTAssertEqual(tunnel.status?.hostConnectionCount?.limit, 5)
        XCTAssertEqual(tunnel.endpoints?.count, 1)
        XCTAssertEqual(tunnel.endpoints?[0].connectionMode, .tunnelRelay)
        XCTAssertEqual(tunnel.endpoints?[0].clientRelayUri, "wss://usw2-data.rel.tunnels.api.visualstudio.com/...")
        XCTAssertEqual(tunnel.ports?.count, 2)
        XCTAssertEqual(tunnel.ports?[0].portNumber, 8080)
        XCTAssertEqual(tunnel.ports?[0].name, "web")
        XCTAssertEqual(tunnel.ports?[0].protocol, .http)
        XCTAssertEqual(tunnel.ports?[1].portNumber, 31546)
    }

    func testTunnelIgnoresUnknownFields() throws {
        let json = """
        {
            "clusterId": "usw2",
            "tunnelId": "abc123",
            "someNewField": "unknown",
            "anotherField": 42
        }
        """
        let tunnel = try JSONDecoder().decode(Tunnel.self, from: Data(json.utf8))
        XCTAssertEqual(tunnel.clusterId, "usw2")
        XCTAssertEqual(tunnel.tunnelId, "abc123")
    }

    func testTunnelMinimalJSON() throws {
        let json = "{}"
        let tunnel = try JSONDecoder().decode(Tunnel.self, from: Data(json.utf8))
        XCTAssertNil(tunnel.clusterId)
        XCTAssertNil(tunnel.tunnelId)
    }

    // MARK: - TunnelEndpoint

    func testEndpointCodableRoundTrip() throws {
        let original = TunnelEndpoint(
            id: "ep-1",
            connectionMode: .tunnelRelay,
            hostId: "host-1",
            hostPublicKeys: ["ssh-ed25519 AAAA..."],
            clientRelayUri: "wss://relay.example.com"
        )
        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(TunnelEndpoint.self, from: data)
        XCTAssertEqual(original, decoded)
    }

    func testEndpointLocalNetwork() throws {
        let json = """
        {
            "connectionMode": "LocalNetwork",
            "hostId": "local-host"
        }
        """
        let ep = try JSONDecoder().decode(TunnelEndpoint.self, from: Data(json.utf8))
        XCTAssertEqual(ep.connectionMode, .localNetwork)
        XCTAssertEqual(ep.hostId, "local-host")
        XCTAssertNil(ep.clientRelayUri)
    }

    func testEndpointPortUriFormat() {
        let ep = TunnelEndpoint(
            portUriFormat: "https://abc123-{port}.usw2.devtunnels.ms"
        )
        let url = ep.portUriFormat?.replacingOccurrences(
            of: tunnelEndpointPortToken, with: "8080"
        )
        XCTAssertEqual(url, "https://abc123-8080.usw2.devtunnels.ms")
    }

    // MARK: - TunnelPort

    func testPortCodableRoundTrip() throws {
        let original = TunnelPort(
            portNumber: 3000,
            name: "dev-server",
            protocol: .https
        )
        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(TunnelPort.self, from: data)
        XCTAssertEqual(original, decoded)
    }

    func testPortMinimalJSON() throws {
        let json = """
        { "portNumber": 443 }
        """
        let port = try JSONDecoder().decode(TunnelPort.self, from: Data(json.utf8))
        XCTAssertEqual(port.portNumber, 443)
        XCTAssertNil(port.name)
        XCTAssertNil(port.protocol)
    }

    func testPortProtocolValues() throws {
        for proto in ["auto", "http", "https", "rdp", "ssh"] {
            let json = """
            { "portNumber": 1, "protocol": "\(proto)" }
            """
            let port = try JSONDecoder().decode(TunnelPort.self, from: Data(json.utf8))
            XCTAssertNotNil(port.protocol, "Protocol '\(proto)' should decode")
        }
    }

    // MARK: - TunnelStatus & ResourceStatus

    func testStatusCodableRoundTrip() throws {
        let original = TunnelStatus(
            portCount: ResourceStatus(current: 3, limit: 10),
            hostConnectionCount: ResourceStatus(current: 1),
            clientConnectionCount: ResourceStatus(current: 0, limit: 100)
        )
        let data = try JSONEncoder().encode(original)
        let decoded = try JSONDecoder().decode(TunnelStatus.self, from: data)
        XCTAssertEqual(original, decoded)
    }

    func testResourceStatusNoLimit() throws {
        let json = """
        { "current": 5 }
        """
        let rs = try JSONDecoder().decode(ResourceStatus.self, from: Data(json.utf8))
        XCTAssertEqual(rs.current, 5)
        XCTAssertNil(rs.limit)
    }

    // MARK: - Enums

    func testConnectionModeValues() throws {
        let jsonRelay = "\"TunnelRelay\""
        let jsonLocal = "\"LocalNetwork\""
        let relay = try JSONDecoder().decode(TunnelConnectionMode.self, from: Data(jsonRelay.utf8))
        let local = try JSONDecoder().decode(TunnelConnectionMode.self, from: Data(jsonLocal.utf8))
        XCTAssertEqual(relay, .tunnelRelay)
        XCTAssertEqual(local, .localNetwork)
    }

    func testAccessScopeConstants() {
        XCTAssertEqual(TunnelAccessScopes.connect, "connect")
        XCTAssertEqual(TunnelAccessScopes.host, "host")
        XCTAssertEqual(TunnelAccessScopes.manage, "manage")
        XCTAssertEqual(TunnelAccessScopes.managePorts, "manage:ports")
        XCTAssertEqual(TunnelAccessScopes.create, "create")
        XCTAssertEqual(TunnelAccessScopes.inspect, "inspect")
    }

    // MARK: - Encoding output

    func testTunnelEncodesCorrectJSONKeys() throws {
        let tunnel = Tunnel(clusterId: "usw2", tunnelId: "t1")
        let data = try JSONEncoder().encode(tunnel)
        let dict = try JSONSerialization.jsonObject(with: data) as! [String: Any]
        XCTAssertNotNil(dict["clusterId"])
        XCTAssertNotNil(dict["tunnelId"])
        // Verify camelCase keys (not snake_case)
        XCTAssertNil(dict["cluster_id"])
        XCTAssertNil(dict["tunnel_id"])
    }
}
