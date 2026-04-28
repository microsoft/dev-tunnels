import XCTest
@testable import DevTunnelsClient
import Foundation

// MARK: - Mock HTTP Client

/// Mock HTTP client that returns canned responses based on URL path.
final class MockHTTPClient: HTTPClient, @unchecked Sendable {
    struct CannedResponse {
        let data: Data
        let statusCode: Int
    }

    private(set) var requests: [URLRequest] = []
    private var responses: [String: CannedResponse] = [:]
    var defaultResponse: CannedResponse?

    func addResponse(pathContains: String, data: Data, statusCode: Int = 200) {
        responses[pathContains] = CannedResponse(data: data, statusCode: statusCode)
    }

    func data(for request: URLRequest) async throws -> (Data, URLResponse) {
        requests.append(request)
        let urlString = request.url?.absoluteString ?? ""
        var response: CannedResponse?
        for (pathKey, cannedResponse) in responses {
            if urlString.contains(pathKey) {
                response = cannedResponse
                break
            }
        }
        let resp = response ?? defaultResponse ?? CannedResponse(data: Data(), statusCode: 200)
        let httpResponse = HTTPURLResponse(
            url: request.url!,
            statusCode: resp.statusCode,
            httpVersion: nil,
            headerFields: nil
        )!
        return (resp.data, httpResponse)
    }
}

// MARK: - Tests

final class ManagementClientTests: XCTestCase {
    let mockHttp = MockHTTPClient()

    func makeClient(token: String = "test-github-token") -> TunnelManagementClient {
        TunnelManagementClient(
            accessToken: token,
            serviceProperties: .production,
            httpClient: mockHttp,
            userAgent: "Test/1.0"
        )
    }

    // MARK: - listTunnels

    func testListTunnelsDecodesRegionResponse() async throws {
        let json = """
        {
            "value": [
                {
                    "regionName": "US West 2",
                    "clusterId": "usw2",
                    "value": [
                        { "tunnelId": "t1", "clusterId": "usw2", "name": "tunnel-one" },
                        { "tunnelId": "t2", "clusterId": "usw2" }
                    ]
                },
                {
                    "regionName": "Europe West",
                    "clusterId": "euw",
                    "value": [
                        { "tunnelId": "t3", "clusterId": "euw", "name": "tunnel-three" }
                    ]
                }
            ]
        }
        """
        mockHttp.addResponse(pathContains: "/tunnels", data: Data(json.utf8))

        let client = makeClient()
        let tunnels = try await client.listTunnels()

        XCTAssertEqual(tunnels.count, 3)
        XCTAssertEqual(tunnels[0].tunnelId, "t1")
        XCTAssertEqual(tunnels[0].name, "tunnel-one")
        XCTAssertEqual(tunnels[1].tunnelId, "t2")
        XCTAssertEqual(tunnels[2].tunnelId, "t3")
        XCTAssertEqual(tunnels[2].clusterId, "euw")
    }

    func testListTunnelsEmptyResponse() async throws {
        let json = """
        { "value": [] }
        """
        mockHttp.addResponse(pathContains: "/tunnels", data: Data(json.utf8))

        let client = makeClient()
        let tunnels = try await client.listTunnels()
        XCTAssertEqual(tunnels.count, 0)
    }

    func testListTunnelsUsesGlobalQueryParam() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient()
        _ = try await client.listTunnels()

        XCTAssertEqual(mockHttp.requests.count, 1)
        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("global=true"), "Should include global=true when no clusterId")
    }

    func testListTunnelsSetsAuthHeader() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient(token: "my-github-token")
        _ = try await client.listTunnels()

        let authHeader = mockHttp.requests[0].value(forHTTPHeaderField: "Authorization")
        XCTAssertEqual(authHeader, "github my-github-token")
    }

    func testAuthHeaderWithSchemePrefix() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient(token: "aad eyJ0eXAiOiJKV1Qi...")
        _ = try await client.listTunnels()

        let authHeader = mockHttp.requests[0].value(forHTTPHeaderField: "Authorization")
        XCTAssertEqual(authHeader, "aad eyJ0eXAiOiJKV1Qi...", "Should use token as-is when scheme prefix present")
    }

    func testListTunnelsIncludesApiVersion() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient()
        _ = try await client.listTunnels()

        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("api-version=2023-09-27-preview"))
    }

    func testListTunnelsHttpErrorThrows() async throws {
        mockHttp.addResponse(
            pathContains: "/tunnels",
            data: Data("Unauthorized".utf8),
            statusCode: 401
        )

        let client = makeClient()
        do {
            _ = try await client.listTunnels()
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, let message) = error {
                XCTAssertEqual(statusCode, 401)
                XCTAssertTrue(message.contains("Unauthorized"))
            } else {
                XCTFail("Wrong error type: \(error)")
            }
        }
    }

    // MARK: - getTunnel

    func testGetTunnelDecodes() async throws {
        let json = """
        {
            "tunnelId": "abc123",
            "clusterId": "usw2",
            "name": "my-tunnel",
            "endpoints": [
                {
                    "connectionMode": "TunnelRelay",
                    "hostId": "host-1",
                    "clientRelayUri": "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123"
                }
            ],
            "ports": [
                { "portNumber": 8080, "name": "web" },
                { "portNumber": 31546 }
            ],
            "accessTokens": {
                "connect": "eyJhbGciOiJSUzI1NiJ9..."
            }
        }
        """
        mockHttp.addResponse(pathContains: "/tunnels/abc123", data: Data(json.utf8))

        let client = makeClient()
        let tunnel = try await client.getTunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            options: TunnelRequestOptions(includePorts: true, tokenScopes: [TunnelAccessScopes.connect])
        )

        XCTAssertEqual(tunnel.tunnelId, "abc123")
        XCTAssertEqual(tunnel.name, "my-tunnel")
        XCTAssertEqual(tunnel.endpoints?.count, 1)
        XCTAssertEqual(tunnel.endpoints?[0].connectionMode, .tunnelRelay)
        XCTAssertEqual(tunnel.endpoints?[0].clientRelayUri, "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123")
        XCTAssertEqual(tunnel.ports?.count, 2)
        XCTAssertEqual(tunnel.ports?[0].portNumber, 8080)
        XCTAssertEqual(tunnel.ports?[1].portNumber, 31546)
        XCTAssertEqual(tunnel.accessTokens?["connect"], "eyJhbGciOiJSUzI1NiJ9...")
    }

    func testGetTunnelUsesClusterSpecificHost() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/t1", data: Data("""
        { "tunnelId": "t1", "clusterId": "usw2" }
        """.utf8))

        let client = makeClient()
        _ = try await client.getTunnel(clusterId: "usw2", tunnelId: "t1")

        let host = mockHttp.requests[0].url!.host()
        XCTAssertEqual(host, "usw2.rel.tunnels.api.visualstudio.com")
    }

    func testGetTunnelIncludesPortsQueryParam() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/t1", data: Data("""
        { "tunnelId": "t1" }
        """.utf8))

        let client = makeClient()
        _ = try await client.getTunnel(
            clusterId: "usw2",
            tunnelId: "t1",
            options: TunnelRequestOptions(includePorts: true)
        )

        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("includePorts=true"))
    }

    func testGetTunnelIncludesTokenScopes() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/t1", data: Data("""
        { "tunnelId": "t1" }
        """.utf8))

        let client = makeClient()
        _ = try await client.getTunnel(
            clusterId: "usw2",
            tunnelId: "t1",
            options: TunnelRequestOptions(tokenScopes: [TunnelAccessScopes.connect])
        )

        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("tokenScopes=connect"))
    }

    func testGetTunnelMultipleTokenScopes() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/t1", data: Data("""
        { "tunnelId": "t1" }
        """.utf8))

        let client = makeClient()
        _ = try await client.getTunnel(
            clusterId: "usw2",
            tunnelId: "t1",
            options: TunnelRequestOptions(tokenScopes: [TunnelAccessScopes.connect, TunnelAccessScopes.host])
        )

        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("tokenScopes=connect"))
        XCTAssertTrue(url.contains("tokenScopes=host"))
    }

    func testGetTunnel404Throws() async throws {
        mockHttp.addResponse(
            pathContains: "/tunnels/missing",
            data: Data("Not Found".utf8),
            statusCode: 404
        )

        let client = makeClient()
        do {
            _ = try await client.getTunnel(clusterId: "usw2", tunnelId: "missing")
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 404)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - Request formatting

    func testRequestIncludesUserAgent() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient()
        _ = try await client.listTunnels()

        let ua = mockHttp.requests[0].value(forHTTPHeaderField: "User-Agent")
        XCTAssertEqual(ua, "Test/1.0")
    }

    func testRequestIncludesAcceptHeader() async throws {
        mockHttp.addResponse(pathContains: "/tunnels", data: Data("""
        { "value": [] }
        """.utf8))

        let client = makeClient()
        _ = try await client.listTunnels()

        let accept = mockHttp.requests[0].value(forHTTPHeaderField: "Accept")
        XCTAssertEqual(accept, "application/json")
    }

    // MARK: - TunnelRequestOptions

    func testRequestOptionsDefaultsEmpty() {
        let opts = TunnelRequestOptions()
        let items = opts.queryItems()
        XCTAssertTrue(items.isEmpty)
    }

    func testRequestOptionsIncludePorts() {
        let opts = TunnelRequestOptions(includePorts: true)
        let items = opts.queryItems()
        XCTAssertEqual(items.count, 1)
        XCTAssertEqual(items[0].name, "includePorts")
        XCTAssertEqual(items[0].value, "true")
    }

    func testRequestOptionsTokenScopes() {
        let opts = TunnelRequestOptions(tokenScopes: ["connect", "host"])
        let items = opts.queryItems()
        XCTAssertEqual(items.count, 2)
        XCTAssertEqual(items[0].name, "tokenScopes")
        XCTAssertEqual(items[0].value, "connect")
        XCTAssertEqual(items[1].name, "tokenScopes")
        XCTAssertEqual(items[1].value, "host")
    }

    // MARK: - Service Properties

    func testProductionServiceProperties() {
        let props = TunnelServiceProperties.production
        XCTAssertEqual(props.serviceUri, "https://global.rel.tunnels.api.visualstudio.com")
        XCTAssertEqual(props.gitHubAppClientId, "Iv1.e7b89e013f801f03")
        XCTAssertEqual(props.apiVersion, "2023-09-27-preview")
    }

    // MARK: - createTunnel

    func testCreateTunnelSendsPutWithBody() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/", data: Data("""
        { "tunnelId": "new-id", "clusterId": "usw2", "name": "my-tunnel" }
        """.utf8))

        let client = makeClient()
        let tunnel = try await client.createTunnel(Tunnel(name: "my-tunnel"))

        XCTAssertEqual(tunnel.tunnelId, "new-id")
        XCTAssertEqual(tunnel.name, "my-tunnel")
        XCTAssertEqual(mockHttp.requests.count, 1)
        XCTAssertEqual(mockHttp.requests[0].httpMethod, "PUT")
        XCTAssertEqual(
            mockHttp.requests[0].value(forHTTPHeaderField: "Content-Type"),
            "application/json"
        )
        XCTAssertEqual(
            mockHttp.requests[0].value(forHTTPHeaderField: "If-Not-Match"),
            "*"
        )
        XCTAssertNotNil(mockHttp.requests[0].httpBody)
        // URL should contain a generated tunnel ID
        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("/tunnels/"), "URL should contain /tunnels/{id}")
    }

    func testCreateTunnelWithOptions() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/", data: Data("""
        { "tunnelId": "t1" }
        """.utf8))

        let client = makeClient()
        _ = try await client.createTunnel(
            Tunnel(name: "test"),
            options: TunnelRequestOptions(tokenScopes: [TunnelAccessScopes.connect])
        )

        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("tokenScopes=connect"))
    }

    func testCreateTunnelHttpErrorThrows() async throws {
        mockHttp.addResponse(
            pathContains: "/tunnels/",
            data: Data("Forbidden".utf8),
            statusCode: 403
        )

        let client = makeClient()
        do {
            _ = try await client.createTunnel(Tunnel(name: "test"))
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 403)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - updateTunnel

    func testUpdateTunnelSendsPut() async throws {
        mockHttp.addResponse(pathContains: "/tunnels/t1", data: Data("""
        { "tunnelId": "t1", "clusterId": "usw2", "description": "updated" }
        """.utf8))

        let client = makeClient()
        let tunnel = try await client.updateTunnel(
            Tunnel(clusterId: "usw2", tunnelId: "t1", description: "updated")
        )

        XCTAssertEqual(tunnel.description, "updated")
        XCTAssertEqual(mockHttp.requests[0].httpMethod, "PUT")
        XCTAssertEqual(
            mockHttp.requests[0].value(forHTTPHeaderField: "If-Match"),
            "*"
        )
    }

    func testUpdateTunnelMissingClusterIdThrows() async throws {
        let client = makeClient()
        do {
            _ = try await client.updateTunnel(Tunnel(tunnelId: "t1"))
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .invalidRequest(let msg) = error {
                XCTAssertTrue(msg.contains("clusterId"))
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    func testUpdateTunnelMissingTunnelIdThrows() async throws {
        let client = makeClient()
        do {
            _ = try await client.updateTunnel(Tunnel(clusterId: "usw2"))
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .invalidRequest(let msg) = error {
                XCTAssertTrue(msg.contains("tunnelId"))
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - deleteTunnel

    func testDeleteTunnelSendsDelete() async throws {
        mockHttp.defaultResponse = MockHTTPClient.CannedResponse(data: Data(), statusCode: 204)

        let client = makeClient()
        try await client.deleteTunnel(clusterId: "usw2", tunnelId: "t1")

        XCTAssertEqual(mockHttp.requests.count, 1)
        XCTAssertEqual(mockHttp.requests[0].httpMethod, "DELETE")
        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("/tunnels/t1"))
    }

    func testDeleteTunnel404Throws() async throws {
        mockHttp.addResponse(
            pathContains: "/tunnels/missing",
            data: Data("Not Found".utf8),
            statusCode: 404
        )

        let client = makeClient()
        do {
            try await client.deleteTunnel(clusterId: "usw2", tunnelId: "missing")
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 404)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - createTunnelPort

    func testCreateTunnelPortSendsPut() async throws {
        mockHttp.addResponse(pathContains: "/ports/", data: Data("""
        { "portNumber": 8080, "name": "web", "protocol": "https" }
        """.utf8))

        let client = makeClient()
        let port = try await client.createTunnelPort(
            clusterId: "usw2",
            tunnelId: "t1",
            port: TunnelPort(portNumber: 8080, name: "web", protocol: .https)
        )

        XCTAssertEqual(port.portNumber, 8080)
        XCTAssertEqual(port.name, "web")
        XCTAssertEqual(mockHttp.requests[0].httpMethod, "PUT")
        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("/tunnels/t1/ports/8080"))
    }

    // MARK: - deleteTunnelPort

    func testDeleteTunnelPortSendsDelete() async throws {
        mockHttp.defaultResponse = MockHTTPClient.CannedResponse(data: Data(), statusCode: 204)

        let client = makeClient()
        try await client.deleteTunnelPort(clusterId: "usw2", tunnelId: "t1", portNumber: 8080)

        XCTAssertEqual(mockHttp.requests[0].httpMethod, "DELETE")
        let url = mockHttp.requests[0].url!.absoluteString
        XCTAssertTrue(url.contains("/tunnels/t1/ports/8080"))
    }
}
