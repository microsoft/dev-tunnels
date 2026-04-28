import XCTest
@testable import DevTunnelsClient

final class TunnelConnectionTests: XCTestCase {

    // MARK: - directURL (from Tunnel)

    func testDirectURLFromTunnel() {
        let tunnel = Tunnel(clusterId: "usw2", tunnelId: "abc123")
        let url = TunnelConnection.directURL(tunnel: tunnel, port: 8080)
        XCTAssertEqual(url?.absoluteString, "wss://abc123-8080.usw2.devtunnels.ms")
    }

    func testDirectURLDifferentPort() {
        let tunnel = Tunnel(clusterId: "euw", tunnelId: "xyz789")
        let url = TunnelConnection.directURL(tunnel: tunnel, port: 31546)
        XCTAssertEqual(url?.absoluteString, "wss://xyz789-31546.euw.devtunnels.ms")
    }

    func testDirectURLNilWhenMissingTunnelId() {
        let tunnel = Tunnel(clusterId: "usw2")
        let url = TunnelConnection.directURL(tunnel: tunnel, port: 8080)
        XCTAssertNil(url)
    }

    func testDirectURLNilWhenMissingClusterId() {
        let tunnel = Tunnel(tunnelId: "abc123")
        let url = TunnelConnection.directURL(tunnel: tunnel, port: 8080)
        XCTAssertNil(url)
    }

    // MARK: - directURL (explicit params)

    func testDirectURLExplicit() {
        let url = TunnelConnection.directURL(tunnelId: "abc", clusterId: "usw2", port: 443)
        XCTAssertEqual(url?.absoluteString, "wss://abc-443.usw2.devtunnels.ms")
    }

    // MARK: - directURL (from endpoint)

    func testDirectURLFromEndpointPortUriFormat() {
        let ep = TunnelEndpoint(
            portUriFormat: "https://abc123-{port}.usw2.devtunnels.ms"
        )
        let url = TunnelConnection.directURL(endpoint: ep, port: 3000)
        XCTAssertEqual(url?.absoluteString, "wss://abc123-3000.usw2.devtunnels.ms")
    }

    func testDirectURLFromEndpointNilWhenNoFormat() {
        let ep = TunnelEndpoint()
        let url = TunnelConnection.directURL(endpoint: ep, port: 3000)
        XCTAssertNil(url)
    }

    // MARK: - connectToken

    func testConnectTokenExtracted() {
        let tunnel = Tunnel(accessTokens: [
            "connect": "eyJ...",
            "manage": "other-token",
        ])
        XCTAssertEqual(TunnelConnection.connectToken(from: tunnel), "eyJ...")
    }

    func testConnectTokenNilWhenMissing() {
        let tunnel = Tunnel(accessTokens: ["manage": "token"])
        XCTAssertNil(TunnelConnection.connectToken(from: tunnel))
    }

    func testConnectTokenNilWhenNoTokens() {
        let tunnel = Tunnel()
        XCTAssertNil(TunnelConnection.connectToken(from: tunnel))
    }

    // MARK: - tunnelAuthHeader

    func testTunnelAuthHeaderFormat() {
        let header = TunnelConnection.tunnelAuthHeader(connectToken: "eyJhbGciOiJSUzI1NiJ9")
        XCTAssertEqual(header, "tunnel eyJhbGciOiJSUzI1NiJ9")
    }

    // MARK: - clientRelayURI

    func testClientRelayURIFromEndpoints() {
        let tunnel = Tunnel(endpoints: [
            TunnelEndpoint(
                connectionMode: .tunnelRelay,
                hostId: "host-1",
                clientRelayUri: "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123"
            ),
        ])
        let uri = TunnelConnection.clientRelayURI(from: tunnel)
        XCTAssertEqual(uri, "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123")
    }

    func testClientRelayURINilWhenLocalNetwork() {
        let tunnel = Tunnel(endpoints: [
            TunnelEndpoint(connectionMode: .localNetwork, hostId: "host-1"),
        ])
        XCTAssertNil(TunnelConnection.clientRelayURI(from: tunnel))
    }

    func testClientRelayURINilWhenNoEndpoints() {
        let tunnel = Tunnel()
        XCTAssertNil(TunnelConnection.clientRelayURI(from: tunnel))
    }

    func testClientRelayURISkipsEndpointsWithoutUri() {
        let tunnel = Tunnel(endpoints: [
            TunnelEndpoint(connectionMode: .tunnelRelay, hostId: "host-1"),
        ])
        XCTAssertNil(TunnelConnection.clientRelayURI(from: tunnel))
    }

    // MARK: - isOnline

    func testIsOnlineWithHostConnections() {
        let tunnel = Tunnel(
            status: TunnelStatus(
                hostConnectionCount: ResourceStatus(current: 1)
            )
        )
        XCTAssertTrue(TunnelConnection.isOnline(tunnel))
    }

    func testIsOnlineWithEndpoints() {
        let tunnel = Tunnel(endpoints: [
            TunnelEndpoint(connectionMode: .tunnelRelay, hostId: "host-1"),
        ])
        XCTAssertTrue(TunnelConnection.isOnline(tunnel))
    }

    func testIsOfflineWithZeroHosts() {
        let tunnel = Tunnel(
            status: TunnelStatus(
                hostConnectionCount: ResourceStatus(current: 0)
            )
        )
        XCTAssertFalse(TunnelConnection.isOnline(tunnel))
    }

    func testIsOfflineWithNoInfo() {
        let tunnel = Tunnel()
        XCTAssertFalse(TunnelConnection.isOnline(tunnel))
    }
}
