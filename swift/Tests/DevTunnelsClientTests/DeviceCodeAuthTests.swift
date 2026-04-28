import XCTest
@testable import DevTunnelsClient
import Foundation

final class DeviceCodeAuthTests: XCTestCase {
    let mockHttp = MockHTTPClient()

    func makeAuth() -> DeviceCodeAuth {
        DeviceCodeAuth(
            serviceProperties: .production,
            httpClient: mockHttp
        )
    }

    // MARK: - start()

    func testStartDecodesResponse() async throws {
        let json = """
        {
            "device_code": "dc-123",
            "user_code": "ABCD-1234",
            "verification_uri": "https://github.com/login/device",
            "expires_in": 900,
            "interval": 5
        }
        """
        mockHttp.addResponse(pathContains: "/login/device/code", data: Data(json.utf8))

        let auth = makeAuth()
        let response = try await auth.start()

        XCTAssertEqual(response.deviceCode, "dc-123")
        XCTAssertEqual(response.userCode, "ABCD-1234")
        XCTAssertEqual(response.verificationUri, "https://github.com/login/device")
        XCTAssertEqual(response.expiresIn, 900)
        XCTAssertEqual(response.interval, 5)
    }

    func testStartSendsCorrectRequest() async throws {
        let json = """
        {
            "device_code": "dc",
            "user_code": "UC",
            "verification_uri": "https://github.com/login/device",
            "expires_in": 900,
            "interval": 5
        }
        """
        mockHttp.addResponse(pathContains: "/login/device/code", data: Data(json.utf8))

        let auth = makeAuth()
        _ = try await auth.start()

        XCTAssertEqual(mockHttp.requests.count, 1)
        let request = mockHttp.requests[0]
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.url?.host(), "github.com")
        XCTAssertEqual(request.url?.path(), "/login/device/code")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Accept"), "application/json")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Content-Type"), "application/x-www-form-urlencoded")

        let body = String(data: request.httpBody ?? Data(), encoding: .utf8) ?? ""
        XCTAssertTrue(body.contains("client_id="))
        XCTAssertTrue(body.contains("scope=user:email"))
    }

    func testStartHttpErrorThrows() async throws {
        mockHttp.addResponse(
            pathContains: "/login/device/code",
            data: Data("Bad Request".utf8),
            statusCode: 400
        )

        let auth = makeAuth()
        do {
            _ = try await auth.start()
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 400)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - poll()

    func testPollReturnsAccessToken() async throws {
        let json = """
        { "access_token": "gho_abc123" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .accessToken("gho_abc123"))
    }

    func testPollReturnsPending() async throws {
        let json = """
        { "error": "authorization_pending" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .pending)
    }

    func testPollSlowDownReturnsPending() async throws {
        let json = """
        { "error": "slow_down" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .pending)
    }

    func testPollReturnsExpired() async throws {
        let json = """
        { "error": "expired_token" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .expired)
    }

    func testPollReturnsErrorWithDescription() async throws {
        let json = """
        { "error": "access_denied", "error_description": "The user denied the request" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .error("The user denied the request"))
    }

    func testPollReturnsErrorWithoutDescription() async throws {
        let json = """
        { "error": "access_denied" }
        """
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data(json.utf8))

        let auth = makeAuth()
        let result = try await auth.poll(deviceCode: "dc-123")

        XCTAssertEqual(result, .error("access_denied"))
    }

    func testPollSendsCorrectRequest() async throws {
        mockHttp.addResponse(pathContains: "/login/oauth/access_token", data: Data("""
        { "error": "authorization_pending" }
        """.utf8))

        let auth = makeAuth()
        _ = try await auth.poll(deviceCode: "my-device-code")

        XCTAssertEqual(mockHttp.requests.count, 1)
        let request = mockHttp.requests[0]
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.url?.path(), "/login/oauth/access_token")

        let body = String(data: request.httpBody ?? Data(), encoding: .utf8) ?? ""
        XCTAssertTrue(body.contains("device_code=my-device-code"))
        XCTAssertTrue(body.contains("grant_type=urn:ietf:params:oauth:grant-type:device_code"))
    }

    func testPollHttpErrorThrows() async throws {
        mockHttp.addResponse(
            pathContains: "/login/oauth/access_token",
            data: Data("Server Error".utf8),
            statusCode: 500
        )

        let auth = makeAuth()
        do {
            _ = try await auth.poll(deviceCode: "dc-123")
            XCTFail("Should have thrown")
        } catch let error as TunnelManagementError {
            if case .httpError(let statusCode, _) = error {
                XCTAssertEqual(statusCode, 500)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        }
    }

    // MARK: - DeviceCodeResponse Codable

    func testDeviceCodeResponseCodable() throws {
        let json = """
        {
            "device_code": "dc",
            "user_code": "UC",
            "verification_uri": "https://example.com",
            "expires_in": 600,
            "interval": 10
        }
        """
        let response = try JSONDecoder().decode(DeviceCodeResponse.self, from: Data(json.utf8))
        XCTAssertEqual(response.deviceCode, "dc")
        XCTAssertEqual(response.userCode, "UC")
        XCTAssertEqual(response.verificationUri, "https://example.com")
        XCTAssertEqual(response.expiresIn, 600)
        XCTAssertEqual(response.interval, 10)

        // Round-trip
        let encoded = try JSONEncoder().encode(response)
        let decoded = try JSONDecoder().decode(DeviceCodeResponse.self, from: encoded)
        XCTAssertEqual(response, decoded)
    }
}
