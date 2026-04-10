import XCTest
@testable import DevTunnelsClient

final class TunnelRelayConfigTests: XCTestCase {

    // MARK: - Config Validation

    func testValidConfigPasses() {
        let config = TunnelRelayConfig(
            relayUri: "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123",
            accessToken: "eyJhbGciOiJSUzI1NiJ9.test",
            port: 8080
        )
        XCTAssertNil(config.validate())
    }

    func testEmptyRelayUriFails() {
        let config = TunnelRelayConfig(relayUri: "", accessToken: "token", port: 8080)
        XCTAssertEqual(config.validate(), .missingRelayUri)
    }

    func testInvalidRelayUriFails() {
        let config = TunnelRelayConfig(relayUri: "not a url", accessToken: "token", port: 8080)
        XCTAssertEqual(config.validate(), .invalidRelayUri("not a url"))
    }

    func testHttpRelayUriFails() {
        let config = TunnelRelayConfig(relayUri: "https://example.com", accessToken: "token", port: 8080)
        XCTAssertEqual(config.validate(), .invalidRelayUri("https://example.com"))
    }

    func testWsRelayUriPasses() {
        let config = TunnelRelayConfig(relayUri: "ws://localhost:8080", accessToken: "token", port: 8080)
        XCTAssertNil(config.validate())
    }

    func testEmptyAccessTokenFails() {
        let config = TunnelRelayConfig(relayUri: "wss://example.com", accessToken: "", port: 8080)
        XCTAssertEqual(config.validate(), .missingAccessToken)
    }

    func testZeroPortFails() {
        let config = TunnelRelayConfig(relayUri: "wss://example.com", accessToken: "token", port: 0)
        XCTAssertEqual(config.validate(), .invalidPort)
    }

    // MARK: - Authorization Header

    func testAuthHeaderPrefixesTunnel() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "eyJ...", port: 1)
        XCTAssertEqual(config.authorizationHeader, "Tunnel eyJ...")
    }

    func testAuthHeaderSkipsPrefixWhenAlreadyPresent() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "Tunnel eyJ...", port: 1)
        XCTAssertEqual(config.authorizationHeader, "Tunnel eyJ...")
    }

    func testAuthHeaderSkipsPrefixLowercase() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "tunnel eyJ...", port: 1)
        XCTAssertEqual(config.authorizationHeader, "tunnel eyJ...")
    }

    // MARK: - Default Values

    func testDefaultSubprotocol() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 1)
        XCTAssertEqual(config.subprotocol, "tunnel-relay-client")
    }

    func testDefaultTimeout() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 1)
        XCTAssertEqual(config.connectionTimeout, 30)
    }

    func testDefaultKeepaliveInterval() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 1)
        XCTAssertEqual(config.keepaliveInterval, 30)
    }

    func testCustomKeepaliveInterval() {
        let config = TunnelRelayConfig(
            relayUri: "wss://x", accessToken: "t", port: 1,
            keepaliveInterval: 60
        )
        XCTAssertEqual(config.keepaliveInterval, 60)
    }

    func testDisabledKeepalive() {
        let config = TunnelRelayConfig(
            relayUri: "wss://x", accessToken: "t", port: 1,
            keepaliveInterval: 0
        )
        XCTAssertEqual(config.keepaliveInterval, 0)
    }

    func testCustomSubprotocol() {
        let config = TunnelRelayConfig(
            relayUri: "wss://x", accessToken: "t", port: 1,
            subprotocol: "tunnel-relay-client-v2-dev"
        )
        XCTAssertEqual(config.subprotocol, "tunnel-relay-client-v2-dev")
    }

    // MARK: - Constants

    func testRelayConstants() {
        XCTAssertEqual(TunnelRelayConstants.clientWebSocketSubProtocol, "tunnel-relay-client")
        XCTAssertEqual(TunnelRelayConstants.portForwardChannelType, "forwarded-tcpip")
        XCTAssertEqual(TunnelRelayConstants.portForwardRequestType, "tcpip-forward")
        XCTAssertEqual(TunnelRelayConstants.sshUser, "tunnel")
        XCTAssertEqual(TunnelRelayConstants.defaultKeepaliveInterval, 30)
    }

    // MARK: - Equatable

    func testConfigEquality() {
        let a = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 8080)
        let b = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 8080)
        let c = TunnelRelayConfig(relayUri: "wss://y", accessToken: "t", port: 8080)
        XCTAssertEqual(a, b)
        XCTAssertNotEqual(a, c)
    }
}

final class RelayConnectionStateTests: XCTestCase {

    func testStateEquality() {
        XCTAssertEqual(RelayConnectionState.disconnected, .disconnected)
        XCTAssertEqual(RelayConnectionState.connectingWebSocket, .connectingWebSocket)
        XCTAssertEqual(RelayConnectionState.connectingSSH, .connectingSSH)
        XCTAssertEqual(RelayConnectionState.openingChannel, .openingChannel)
        XCTAssertEqual(RelayConnectionState.connected, .connected)
        XCTAssertEqual(RelayConnectionState.closed, .closed)
    }

    func testStateInequality() {
        XCTAssertNotEqual(RelayConnectionState.disconnected, .connected)
        XCTAssertNotEqual(RelayConnectionState.connectingWebSocket, .connectingSSH)
    }

    func testFailedStateEquality() {
        let err1 = RelayConnectionError.timeout
        let err2 = RelayConnectionError.timeout
        XCTAssertEqual(RelayConnectionState.failed(err1), .failed(err2))
    }

    func testFailedStateDifferentErrors() {
        XCTAssertNotEqual(
            RelayConnectionState.failed(.timeout),
            .failed(.sshFailed("error"))
        )
    }

    func testReconnectingStateEquality() {
        XCTAssertEqual(
            RelayConnectionState.reconnecting(attempt: 1),
            .reconnecting(attempt: 1)
        )
        XCTAssertNotEqual(
            RelayConnectionState.reconnecting(attempt: 1),
            .reconnecting(attempt: 2)
        )
    }

    func testReconnectingNotEqualToOtherStates() {
        XCTAssertNotEqual(RelayConnectionState.reconnecting(attempt: 1), .connected)
        XCTAssertNotEqual(RelayConnectionState.reconnecting(attempt: 1), .disconnected)
    }

    func testReconnectFailedError() {
        let err = RelayConnectionError.reconnectFailed(attempts: 5)
        XCTAssertEqual(err, .reconnectFailed(attempts: 5))
        XCTAssertNotEqual(err, .reconnectFailed(attempts: 3))
    }
}

final class TunnelRelayClientTests: XCTestCase {

    func testInitialStateIsDisconnected() {
        let config = TunnelRelayConfig(relayUri: "wss://x", accessToken: "t", port: 8080)
        let client = TunnelRelayClient(config: config)
        XCTAssertEqual(client.state, .disconnected)
    }

    func testValidateConfigDetectsErrors() {
        let config = TunnelRelayConfig(relayUri: "", accessToken: "t", port: 8080)
        let client = TunnelRelayClient(config: config)
        XCTAssertEqual(client.validateConfig(), .missingRelayUri)
    }

    func testValidateConfigPassesForValid() {
        let config = TunnelRelayConfig(
            relayUri: "wss://usw2.example.com/tunnel",
            accessToken: "eyJhbGciOiJSUzI1NiJ9",
            port: 31546
        )
        let client = TunnelRelayClient(config: config)
        XCTAssertNil(client.validateConfig())
    }

    func testConnectWithInvalidConfigTransitionsToFailed() async {
        let config = TunnelRelayConfig(relayUri: "", accessToken: "t", port: 8080)
        let client = TunnelRelayClient(config: config)

        do {
            _ = try await client.connect()
            XCTFail("Should have thrown")
        } catch let error as RelayConnectionError {
            if case .invalidConfig(let configErr) = error {
                XCTAssertEqual(configErr, .missingRelayUri)
            } else {
                XCTFail("Wrong error: \(error)")
            }
        } catch {
            XCTFail("Unexpected error type: \(error)")
        }
        XCTAssertEqual(client.state, .failed(.invalidConfig(.missingRelayUri)))
    }

    func testFromTunnelWithRelayEndpoint() {
        let tunnel = Tunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            accessTokens: ["connect": "eyJ-token"],
            endpoints: [
                TunnelEndpoint(
                    connectionMode: .tunnelRelay,
                    hostId: "host-1",
                    clientRelayUri: "wss://usw2-data.rel.tunnels.api.visualstudio.com/abc123"
                ),
            ]
        )
        let client = TunnelRelayClient.fromTunnel(tunnel, port: 8080)
        XCTAssertNotNil(client)
        XCTAssertEqual(client?.state, .disconnected)
    }

    func testFromTunnelReturnsNilWithoutRelayUri() {
        let tunnel = Tunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            accessTokens: ["connect": "token"],
            endpoints: [
                TunnelEndpoint(connectionMode: .localNetwork, hostId: "host-1"),
            ]
        )
        let client = TunnelRelayClient.fromTunnel(tunnel, port: 8080)
        XCTAssertNil(client)
    }

    func testFromTunnelReturnsNilWithoutConnectToken() {
        let tunnel = Tunnel(
            clusterId: "usw2",
            tunnelId: "abc123",
            accessTokens: ["manage": "token"],
            endpoints: [
                TunnelEndpoint(
                    connectionMode: .tunnelRelay,
                    hostId: "host-1",
                    clientRelayUri: "wss://example.com/relay"
                ),
            ]
        )
        let client = TunnelRelayClient.fromTunnel(tunnel, port: 8080)
        XCTAssertNil(client)
    }

    func testDisconnectTransitionsToClosed() {
        let config = TunnelRelayConfig(
            relayUri: "wss://example.com",
            accessToken: "token",
            port: 8080
        )
        let client = TunnelRelayClient(config: config)
        client.disconnect()
        XCTAssertEqual(client.state, .closed)
    }

    func testOnStateChangeHandlerCalledOnTransition() async {
        let config = TunnelRelayConfig(relayUri: "", accessToken: "t", port: 8080)
        let client = TunnelRelayClient(config: config)

        var observedStates: [RelayConnectionState] = []
        client.onStateChangeHandler = { state in
            observedStates.append(state)
        }

        // connect() with invalid config triggers transition to .failed
        _ = try? await client.connect()

        XCTAssertTrue(observedStates.contains(.failed(.invalidConfig(.missingRelayUri))))
    }

    func testDisconnectTriggersStateChange() {
        let config = TunnelRelayConfig(
            relayUri: "wss://example.com",
            accessToken: "token",
            port: 8080
        )
        let client = TunnelRelayClient(config: config)

        var observedStates: [RelayConnectionState] = []
        client.onStateChangeHandler = { state in
            observedStates.append(state)
        }

        client.disconnect()
        XCTAssertEqual(observedStates, [.closed])
    }
}

// MARK: - ReconnectPolicy Tests

final class ReconnectPolicyTests: XCTestCase {

    func testDefaultPolicy() {
        let policy = ReconnectPolicy.default
        XCTAssertEqual(policy.maxAttempts, 5)
        XCTAssertEqual(policy.initialDelay, 1)
        XCTAssertEqual(policy.maxDelay, 30)
        XCTAssertEqual(policy.backoffMultiplier, 2.0)
    }

    func testDisabledPolicy() {
        let policy = ReconnectPolicy.disabled
        XCTAssertEqual(policy.maxAttempts, 0)
    }

    func testExponentialBackoff() {
        let policy = ReconnectPolicy(
            initialDelay: 1,
            maxDelay: 60,
            backoffMultiplier: 2.0
        )
        XCTAssertEqual(policy.delay(forAttempt: 0), 1)     // 1 * 2^0 = 1
        XCTAssertEqual(policy.delay(forAttempt: 1), 2)     // 1 * 2^1 = 2
        XCTAssertEqual(policy.delay(forAttempt: 2), 4)     // 1 * 2^2 = 4
        XCTAssertEqual(policy.delay(forAttempt: 3), 8)     // 1 * 2^3 = 8
        XCTAssertEqual(policy.delay(forAttempt: 4), 16)    // 1 * 2^4 = 16
    }

    func testBackoffCappedAtMax() {
        let policy = ReconnectPolicy(
            initialDelay: 1,
            maxDelay: 10,
            backoffMultiplier: 2.0
        )
        XCTAssertEqual(policy.delay(forAttempt: 0), 1)
        XCTAssertEqual(policy.delay(forAttempt: 3), 8)
        XCTAssertEqual(policy.delay(forAttempt: 4), 10)  // capped at maxDelay
        XCTAssertEqual(policy.delay(forAttempt: 10), 10) // still capped
    }

    func testCustomPolicy() {
        let policy = ReconnectPolicy(
            maxAttempts: 3,
            initialDelay: 0.5,
            maxDelay: 5,
            backoffMultiplier: 3.0
        )
        XCTAssertEqual(policy.maxAttempts, 3)
        XCTAssertEqual(policy.delay(forAttempt: 0), 0.5)    // 0.5 * 3^0 = 0.5
        XCTAssertEqual(policy.delay(forAttempt: 1), 1.5)    // 0.5 * 3^1 = 1.5
        XCTAssertEqual(policy.delay(forAttempt: 2), 4.5)    // 0.5 * 3^2 = 4.5
        XCTAssertEqual(policy.delay(forAttempt: 3), 5.0)    // 0.5 * 3^3 = 13.5 → capped at 5
    }

    func testPolicyEquality() {
        let a = ReconnectPolicy(maxAttempts: 5, initialDelay: 1, maxDelay: 30, backoffMultiplier: 2.0)
        let b = ReconnectPolicy(maxAttempts: 5, initialDelay: 1, maxDelay: 30, backoffMultiplier: 2.0)
        let c = ReconnectPolicy(maxAttempts: 3)
        XCTAssertEqual(a, b)
        XCTAssertNotEqual(a, c)
    }
}
