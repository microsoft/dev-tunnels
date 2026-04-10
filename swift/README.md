# dev-tunnels-swift

A pure Swift client library for [Microsoft Dev Tunnels](https://aka.ms/devtunnels/docs). Connect to tunnel forwarded ports from iOS and macOS apps.

## Features

- **Tunnel management** — Full CRUD: list, get, create, update, delete tunnels and ports via the REST API
- **GitHub authentication** — Device code flow for tunnel access tokens
- **Direct connections** — Connect to publicly accessible tunnel ports via HTTPS
- **Relay connections** — Connect to private tunnel ports via WebSocket + SSH port forwarding
- **Auto-reconnect** — Configurable reconnection with exponential backoff on connection drops
- **Keepalive** — Periodic WebSocket pings to prevent idle connection drops
- **TLS** — Native Apple TLS via Network.framework for secure relay connections
- **Pure Swift** — No FFI, no Rust, no cross-compilation — just a Swift Package

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Your App                          │
├─────────────────────────────────────────────────────┤
│  DeviceCodeAuth       TunnelManagementClient        │  ← Management layer
│  (GitHub OAuth)       (REST: list/get/create/update/ │
│                        delete tunnels & ports)       │
├─────────────────────────────────────────────────────┤
│  TunnelConnection                                   │  ← Connection helpers
│  (direct URLs, token extraction, online detection)  │
├─────────────────────────────────────────────────────┤
│  TunnelRelayClient                                  │  ← Relay client
│  (public API: connect/disconnect, state machine)    │
├─────────────────────────────────────────────────────┤
│  TunnelRelayStream                                  │  ← NIO pipeline
│  ┌───────────────────────────────────────────────┐  │
│  │  NIOTSConnectionBootstrap (TLS for wss://)    │  │
│  │    → WebSocketUpgradeHandler (HTTP → WS)      │  │
│  │      → WebSocketBinaryFrameHandler            │  │
│  │        → NIOSSHHandler (user: "tunnel")        │  │
│  │          → forwardedTCPIP channel             │  │
│  │            → SSHPortForwardDataHandler        │  │
│  └───────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────┤
│  Contracts: Tunnel, TunnelEndpoint, TunnelPort,     │  ← Types
│  TunnelStatus, enums (Codable, Sendable)            │
└─────────────────────────────────────────────────────┘
```

### Source Layout

```
Sources/DevTunnelsClient/
├── Contracts/          Tunnel, TunnelEndpoint, TunnelPort, TunnelStatus, enums
├── Management/         TunnelManagementClient, DeviceCodeAuth, HTTPClient protocol
└── Connections/        TunnelRelayClient, TunnelRelayStream, port forward messages
```

### How Relay Connections Work

1. **WebSocket** — Connect to the relay URI (`wss://`) with subprotocol `tunnel-relay-client` and `Authorization: Tunnel <accessToken>` header
2. **SSH over WebSocket** — Binary WebSocket frames carry SSH protocol data. SSH authenticates as user `tunnel` with no password (the access token provides auth)
3. **Port forwarding** — Open a `forwarded-tcpip` SSH channel targeting `127.0.0.1:<port>` on the tunnel host
4. **Data streaming** — Bidirectional data flows through the SSH channel

## Installation

Add to your `Package.swift`:

```swift
dependencies: [
    .package(url: "https://github.com/rebornix/dev-tunnels-swift.git", from: "0.1.0"),
]
```

## Quick Start

### Authentication + Discovery

```swift
import DevTunnelsClient

// Authenticate via GitHub device code flow
let auth = try await DeviceCodeAuth.start()
print("Go to \(auth.verificationUri) and enter: \(auth.userCode)")
let token = try await DeviceCodeAuth.poll(deviceCode: auth.deviceCode)

// List tunnels
let client = TunnelManagementClient(accessToken: token)
let tunnels = try await client.listTunnels()

// Get tunnel detail with connect token
let detail = try await client.getTunnel(
    clusterId: "usw2",
    tunnelId: "my-tunnel",
    tokenScopes: [TunnelAccessScopes.connect]
)
```

### Tunnel CRUD

```swift
// Create a tunnel
let newTunnel = try await client.createTunnel(Tunnel(name: "my-app"))

// Update a tunnel
var updated = newTunnel
updated.description = "Production endpoint"
let result = try await client.updateTunnel(updated)

// Add a port
let port = try await client.createTunnelPort(
    clusterId: newTunnel.clusterId!,
    tunnelId: newTunnel.tunnelId!,
    port: TunnelPort(portNumber: 8080, protocol: .https)
)

// Delete a port
try await client.deleteTunnelPort(
    clusterId: newTunnel.clusterId!,
    tunnelId: newTunnel.tunnelId!,
    portNumber: 8080
)

// Delete a tunnel
try await client.deleteTunnel(
    clusterId: newTunnel.clusterId!,
    tunnelId: newTunnel.tunnelId!
)
```

### Direct Connection (Public Ports)

```swift
// For public tunnel ports — just use the direct URL
if let url = TunnelConnection.directURL(from: tunnel, port: 8080) {
    // Use URLSession, WKWebView, etc. with this URL
}
```

### Relay Connection (Private Ports)

```swift
// For private tunnel ports — connect through the relay
if let relay = TunnelRelayClient.fromTunnel(detail, port: 8080) {
    let stream = try await relay.connect()
    // stream.send(data) / stream.close()
}
```

### Auto-Reconnecting Connection

```swift
// Automatically reconnect on connection drops
let relay = TunnelRelayClient(config: config)

// Observe state changes
relay.onStateChangeHandler = { state in
    print("State: \(state)")  // .connected, .reconnecting(attempt: 1), etc.
}

// Each iteration yields a new stream after (re)connection
for await stream in relay.connectWithReconnect() {
    // Use stream until it disconnects; loop yields a new one
}

// Custom retry policy
let policy = ReconnectPolicy(
    maxAttempts: 10,
    initialDelay: 0.5,
    maxDelay: 60,
    backoffMultiplier: 2.0
)
for await stream in relay.connectWithReconnect(policy: policy) {
    // ...
}
```

## Limitations

> **This library is under active development.** The following limitations apply to the current version.

### Not Yet Implemented

- **Server-initiated port notifications** — The SSH `tcpip-forward` global request (server telling the client which ports are available) is not yet handled. The client must know the port number in advance.
- **Local TCP listener** — The Go/TS SDKs can open a local TCP socket and forward connections to the tunnel. This library provides the raw stream; local listener forwarding is the caller's responsibility.
- **Host-side functionality** — This is a client-only library. Hosting a tunnel (registering ports, accepting connections) is out of scope.

### Known Constraints

- **Apple platforms only for TLS** — TLS uses `NIOTransportServices` (Network.framework), which requires iOS/macOS. Non-Apple platforms would need NIOSSL instead.
- **No certificate pinning** — The relay connection trusts the system TLS certificate store. The SSH layer accepts any host key (matching the Go SDK's `InsecureIgnoreHostKey` behavior, since auth is via the tunnel access token).
- **Single-port streams** — Each `TunnelRelayClient` connects to one port. To forward multiple ports, create multiple clients.

## Dependencies

| Package | Purpose |
|---|---|
| [swift-nio](https://github.com/apple/swift-nio) | Async networking, WebSocket codec |
| [swift-nio-ssh](https://github.com/apple/swift-nio-ssh) | SSH protocol over WebSocket |
| [swift-nio-transport-services](https://github.com/apple/swift-nio-transport-services) | Apple TLS via Network.framework |

## Requirements

- iOS 16+ / macOS 13+
- Swift 5.9+

## Testing

```bash
swift test    # 155 tests, all offline (no network requests)
```

All tests use mock HTTP clients and NIO `EmbeddedChannel` — no real network calls are made during testing.
