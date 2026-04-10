import Foundation

/// Utility for constructing Dev Tunnels connection URLs.
///
/// Two connection approaches:
/// 1. **Direct HTTPS** — Connect via `{tunnelId}-{port}.{clusterId}.devtunnels.ms`
///    with a connect access token. Simple but only works for publicly accessible tunnels.
/// 2. **Relay** — Connect via the relay WebSocket URI (SSH-over-WebSocket).
///    Works for private tunnels. (Not yet implemented)
public enum TunnelConnection {

    /// Builds the direct HTTPS WebSocket URL for a forwarded port.
    ///
    /// Format: `wss://{tunnelId}-{port}.{clusterId}.devtunnels.ms`
    ///
    /// - Parameters:
    ///   - tunnel: Tunnel with clusterId and tunnelId.
    ///   - port: Port number to connect to.
    /// - Returns: WebSocket URL, or nil if tunnel is missing required fields.
    public static func directURL(tunnel: Tunnel, port: UInt16) -> URL? {
        guard let tunnelId = tunnel.tunnelId,
              let clusterId = tunnel.clusterId else {
            return nil
        }
        return directURL(tunnelId: tunnelId, clusterId: clusterId, port: port)
    }

    /// Builds the direct HTTPS WebSocket URL from explicit parameters.
    ///
    /// - Parameters:
    ///   - tunnelId: The tunnel ID.
    ///   - clusterId: The cluster ID.
    ///   - port: Port number to connect to.
    /// - Returns: WebSocket URL.
    public static func directURL(tunnelId: String, clusterId: String, port: UInt16) -> URL? {
        URL(string: "wss://\(tunnelId)-\(port).\(clusterId).devtunnels.ms")
    }

    /// Builds the direct URL from a tunnel endpoint's portUriFormat.
    ///
    /// The portUriFormat contains `{port}` which gets replaced with the actual port number.
    ///
    /// - Parameters:
    ///   - endpoint: Tunnel endpoint with portUriFormat.
    ///   - port: Port number.
    /// - Returns: URL with port substituted, or nil if endpoint has no portUriFormat.
    public static func directURL(endpoint: TunnelEndpoint, port: UInt16) -> URL? {
        guard let format = endpoint.portUriFormat else { return nil }
        let urlString = format.replacingOccurrences(
            of: tunnelEndpointPortToken,
            with: String(port)
        )
        // Convert https:// to wss:// for WebSocket
        let wsUrlString = urlString
            .replacingOccurrences(of: "https://", with: "wss://")
            .replacingOccurrences(of: "http://", with: "ws://")
        return URL(string: wsUrlString)
    }

    /// Extracts the connect access token from a tunnel's accessTokens.
    ///
    /// - Parameter tunnel: Tunnel with accessTokens.
    /// - Returns: The connect-scoped JWT, or nil if not present.
    public static func connectToken(from tunnel: Tunnel) -> String? {
        tunnel.accessTokens?[TunnelAccessScopes.connect]
    }

    /// Builds the authorization header value for tunnel connect.
    ///
    /// - Parameter connectToken: The connect-scoped JWT.
    /// - Returns: Header value in the format `tunnel {token}`.
    public static func tunnelAuthHeader(connectToken: String) -> String {
        "tunnel \(connectToken)"
    }

    /// Extracts the client relay URI from a tunnel's endpoints.
    ///
    /// Looks for a TunnelRelay endpoint with a clientRelayUri.
    ///
    /// - Parameter tunnel: Tunnel with endpoints.
    /// - Returns: The client relay URI, or nil if no relay endpoint exists.
    public static func clientRelayURI(from tunnel: Tunnel) -> String? {
        tunnel.endpoints?
            .first(where: { $0.connectionMode == .tunnelRelay && $0.clientRelayUri != nil })?
            .clientRelayUri
    }

    /// Checks whether a tunnel currently has active host connections.
    ///
    /// - Parameter tunnel: Tunnel with status or endpoints.
    /// - Returns: true if hosts are connected.
    public static func isOnline(_ tunnel: Tunnel) -> Bool {
        if let count = tunnel.status?.hostConnectionCount?.current, count > 0 {
            return true
        }
        if let endpoints = tunnel.endpoints, !endpoints.isEmpty {
            return true
        }
        return false
    }
}
