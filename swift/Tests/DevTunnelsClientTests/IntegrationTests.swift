import XCTest
@testable import DevTunnelsClient
import Foundation

/// Live integration tests for the tunnel management API.
///
/// These tests run against the real Dev Tunnels service and require authentication.
/// They are **skipped** when no token is configured.
///
/// To run:
/// 1. Install devtunnels CLI: `brew install --cask devtunnel`
/// 2. Login: `devtunnel user login`
/// 3. Get token: `devtunnel user show --verbose` (copy the full token line, e.g. "github <token>")
/// 4. Run:
///    ```
///    DEV_TUNNELS_TOKEN="github ghp_xxxx" swift test --filter IntegrationTests
///    ```
final class IntegrationTests: XCTestCase {

    private static func userToken() -> String? {
        let token = ProcessInfo.processInfo.environment["DEV_TUNNELS_TOKEN"]
        if let token, !token.isEmpty {
            return token
        }
        return nil
    }

    private func skipIfNoToken() throws -> String {
        guard let token = Self.userToken() else {
            throw XCTSkip("No DEV_TUNNELS_TOKEN set — skipping live integration test")
        }
        return token
    }

    // MARK: - Full tunnel CRUD lifecycle

    func testTunnelCRUDLifecycle() async throws {
        let token = try skipIfNoToken()
        let client = TunnelManagementClient(accessToken: token)

        // 1. Create a tunnel
        let created = try await client.createTunnel(
            Tunnel(description: "swift-sdk-integration-test"),
            options: TunnelRequestOptions(tokenScopes: [TunnelAccessScopes.manage])
        )
        XCTAssertNotNil(created.tunnelId, "Server should assign a tunnelId")
        XCTAssertNotNil(created.clusterId, "Server should assign a clusterId")

        let tunnelId = created.tunnelId!
        let clusterId = created.clusterId!

        // Cleanup: always delete at the end
        addTeardownBlock {
            try? await client.deleteTunnel(clusterId: clusterId, tunnelId: tunnelId)
        }

        // 2. Get the tunnel back
        let fetched = try await client.getTunnel(
            clusterId: clusterId,
            tunnelId: tunnelId,
            options: TunnelRequestOptions(includePorts: true)
        )
        XCTAssertEqual(fetched.tunnelId, tunnelId)
        XCTAssertEqual(fetched.description, "swift-sdk-integration-test")

        // 3. Update the tunnel
        var toUpdate = fetched
        toUpdate.description = "updated-by-swift-sdk"
        let updated = try await client.updateTunnel(toUpdate)
        XCTAssertEqual(updated.description, "updated-by-swift-sdk")

        // 4. Create a port
        let port = try await client.createTunnelPort(
            clusterId: clusterId,
            tunnelId: tunnelId,
            port: TunnelPort(portNumber: 8080, name: "web", protocol: .https)
        )
        XCTAssertEqual(port.portNumber, 8080)

        // 5. Verify port appears on tunnel
        let withPorts = try await client.getTunnel(
            clusterId: clusterId,
            tunnelId: tunnelId,
            options: TunnelRequestOptions(includePorts: true)
        )
        XCTAssertEqual(withPorts.ports?.count, 1)
        XCTAssertEqual(withPorts.ports?.first?.portNumber, 8080)

        // 6. Delete the port
        try await client.deleteTunnelPort(
            clusterId: clusterId,
            tunnelId: tunnelId,
            portNumber: 8080
        )

        // 7. Verify port is gone
        let afterPortDelete = try await client.getTunnel(
            clusterId: clusterId,
            tunnelId: tunnelId,
            options: TunnelRequestOptions(includePorts: true)
        )
        XCTAssertEqual(afterPortDelete.ports?.count ?? 0, 0)

        // 8. Delete the tunnel
        try await client.deleteTunnel(clusterId: clusterId, tunnelId: tunnelId)

        // 9. Verify tunnel is gone (should 404)
        do {
            _ = try await client.getTunnel(clusterId: clusterId, tunnelId: tunnelId)
            XCTFail("Should have thrown 404 after delete")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 404)
            }
        }
    }

    // MARK: - List tunnels

    func testListTunnels() async throws {
        let token = try skipIfNoToken()
        let client = TunnelManagementClient(accessToken: token)

        let tunnels = try await client.listTunnels()
        // Just verify it doesn't throw and returns an array
        XCTAssertTrue(tunnels.count >= 0)
    }
}
