import Foundation

/// Client for the Dev Tunnels management REST API.
///
/// Supports listing, creating, updating, and deleting tunnels and ports.
/// Uses protocol-based HTTP abstraction for testability.
public struct TunnelManagementClient: Sendable {
    private let httpClient: any HTTPClient
    private let accessToken: String
    private let serviceProperties: TunnelServiceProperties
    private let userAgent: String

    /// Creates a new management client.
    ///
    /// - Parameters:
    ///   - accessToken: Authentication token. Can be either:
    ///     - A full Authorization header value with scheme prefix (e.g. `"github <token>"`, `"aad <token>"`)
    ///     - A raw GitHub token (will be prefixed with `"github "` automatically)
    ///   - serviceProperties: Service endpoint configuration. Defaults to production.
    ///   - httpClient: HTTP client for requests. Defaults to URLSession.shared.
    ///   - userAgent: User-Agent string for requests.
    public init(
        accessToken: String,
        serviceProperties: TunnelServiceProperties = .production,
        httpClient: any HTTPClient = URLSession.shared,
        userAgent: String = "Dev-Tunnels-Swift-Client/0.1.0"
    ) {
        self.accessToken = accessToken
        self.serviceProperties = serviceProperties
        self.httpClient = httpClient
        self.userAgent = userAgent
    }

    // MARK: - Tunnel operations

    /// Lists all tunnels accessible to the authenticated user.
    ///
    /// - Parameter clusterId: Optional cluster ID to scope the request. If nil, lists globally.
    /// - Returns: Array of tunnels across all regions.
    public func listTunnels(clusterId: String? = nil) async throws -> [Tunnel] {
        var queryItems = [URLQueryItem]()
        if clusterId == nil {
            queryItems.append(URLQueryItem(name: "global", value: "true"))
        }
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(clusterId: clusterId, path: "/tunnels", queryItems: queryItems)
        let request = buildRequest(url: url, method: "GET")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)

        do {
            let regionResponse = try JSONDecoder().decode(TunnelListByRegionResponse.self, from: data)
            var tunnels = [Tunnel]()
            for region in regionResponse.value ?? [] {
                tunnels.append(contentsOf: region.value ?? [])
            }
            return tunnels
        } catch {
            throw TunnelManagementError.decodingError("Failed to decode tunnel list: \(error)")
        }
    }

    /// Gets detailed information about a specific tunnel.
    ///
    /// - Parameters:
    ///   - clusterId: Cluster ID where the tunnel lives.
    ///   - tunnelId: The tunnel ID.
    ///   - options: Additional request options (ports, token scopes, etc.).
    /// - Returns: Tunnel with requested details (ports, endpoints, access tokens).
    public func getTunnel(
        clusterId: String,
        tunnelId: String,
        options: TunnelRequestOptions = TunnelRequestOptions()
    ) async throws -> Tunnel {
        var queryItems = options.queryItems()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(clusterId: clusterId, path: "/tunnels/\(tunnelId)", queryItems: queryItems)
        let request = buildRequest(url: url, method: "GET")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)

        do {
            return try JSONDecoder().decode(Tunnel.self, from: data)
        } catch {
            throw TunnelManagementError.decodingError("Failed to decode tunnel: \(error)")
        }
    }

    private static let createNameRetries = 3

    /// Creates a new tunnel.
    ///
    /// Generates a tunnel ID client-side and uses PUT with `If-Not-Match: *`
    /// to ensure creation (not update). Retries with a new ID on 409 Conflict.
    ///
    /// - Parameters:
    ///   - tunnel: Tunnel properties to set (name, description, labels, etc.).
    ///   - options: Additional request options (token scopes, etc.).
    /// - Returns: The created tunnel with server-assigned cluster.
    public func createTunnel(
        _ tunnel: Tunnel,
        options: TunnelRequestOptions = TunnelRequestOptions()
    ) async throws -> Tunnel {
        var tunnel = tunnel
        let idGenerated = tunnel.tunnelId == nil || tunnel.tunnelId!.isEmpty
        if idGenerated {
            tunnel.tunnelId = Self.generateTunnelId()
        }

        var queryItems = options.queryItems()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        for retry in 0..<Self.createNameRetries {
            let url = try buildURL(clusterId: tunnel.clusterId, path: "/tunnels/\(tunnel.tunnelId!)", queryItems: queryItems)
            var request = buildRequest(url: url, method: "PUT")
            request.httpBody = try JSONEncoder().encode(Self.tunnelForRequest(tunnel))
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
            request.setValue("*", forHTTPHeaderField: "If-Not-Match")

            let (data, response) = try await httpClient.data(for: request)

            if let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 409 {
                if idGenerated && retry < Self.createNameRetries - 1 {
                    tunnel.tunnelId = Self.generateTunnelId()
                    continue
                }
            }

            try checkResponse(response, data: data)

            do {
                return try JSONDecoder().decode(Tunnel.self, from: data)
            } catch {
                throw TunnelManagementError.decodingError("Failed to decode created tunnel: \(error)")
            }
        }

        throw TunnelManagementError.invalidRequest("Failed to create tunnel after \(Self.createNameRetries) retries")
    }

    /// Updates an existing tunnel.
    ///
    /// Uses PUT with `If-Match: *` to ensure the tunnel exists.
    ///
    /// - Parameters:
    ///   - tunnel: Tunnel with `clusterId` and `tunnelId` set, plus fields to update.
    ///   - options: Additional request options.
    /// - Returns: The updated tunnel.
    /// - Throws: `TunnelManagementError.invalidRequest` if `clusterId` or `tunnelId` is missing.
    public func updateTunnel(
        _ tunnel: Tunnel,
        options: TunnelRequestOptions = TunnelRequestOptions()
    ) async throws -> Tunnel {
        guard let clusterId = tunnel.clusterId, !clusterId.isEmpty else {
            throw TunnelManagementError.invalidRequest("clusterId is required for update")
        }
        guard let tunnelId = tunnel.tunnelId, !tunnelId.isEmpty else {
            throw TunnelManagementError.invalidRequest("tunnelId is required for update")
        }

        var queryItems = options.queryItems()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(clusterId: clusterId, path: "/tunnels/\(tunnelId)", queryItems: queryItems)
        var request = buildRequest(url: url, method: "PUT")
        request.httpBody = try JSONEncoder().encode(Self.tunnelForRequest(tunnel))
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("*", forHTTPHeaderField: "If-Match")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)

        do {
            return try JSONDecoder().decode(Tunnel.self, from: data)
        } catch {
            throw TunnelManagementError.decodingError("Failed to decode updated tunnel: \(error)")
        }
    }

    /// Deletes a tunnel.
    ///
    /// - Parameters:
    ///   - clusterId: Cluster ID where the tunnel lives.
    ///   - tunnelId: The tunnel ID to delete.
    public func deleteTunnel(
        clusterId: String,
        tunnelId: String
    ) async throws {
        var queryItems = [URLQueryItem]()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(clusterId: clusterId, path: "/tunnels/\(tunnelId)", queryItems: queryItems)
        let request = buildRequest(url: url, method: "DELETE")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)
    }

    // MARK: - Port operations

    /// Creates or updates a port on a tunnel.
    ///
    /// - Parameters:
    ///   - clusterId: Cluster ID where the tunnel lives.
    ///   - tunnelId: The tunnel ID.
    ///   - port: Port properties to set.
    ///   - options: Additional request options.
    /// - Returns: The created or updated port.
    public func createTunnelPort(
        clusterId: String,
        tunnelId: String,
        port: TunnelPort,
        options: TunnelRequestOptions = TunnelRequestOptions()
    ) async throws -> TunnelPort {
        var queryItems = options.queryItems()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(
            clusterId: clusterId,
            path: "/tunnels/\(tunnelId)/ports/\(port.portNumber)",
            queryItems: queryItems
        )
        var request = buildRequest(url: url, method: "PUT")
        request.httpBody = try JSONEncoder().encode(port)
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("*", forHTTPHeaderField: "If-Not-Match")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)

        do {
            return try JSONDecoder().decode(TunnelPort.self, from: data)
        } catch {
            throw TunnelManagementError.decodingError("Failed to decode tunnel port: \(error)")
        }
    }

    /// Deletes a port from a tunnel.
    ///
    /// - Parameters:
    ///   - clusterId: Cluster ID where the tunnel lives.
    ///   - tunnelId: The tunnel ID.
    ///   - portNumber: The port number to delete.
    public func deleteTunnelPort(
        clusterId: String,
        tunnelId: String,
        portNumber: UInt16
    ) async throws {
        var queryItems = [URLQueryItem]()
        queryItems.append(URLQueryItem(name: "api-version", value: serviceProperties.apiVersion))

        let url = try buildURL(
            clusterId: clusterId,
            path: "/tunnels/\(tunnelId)/ports/\(portNumber)",
            queryItems: queryItems
        )
        let request = buildRequest(url: url, method: "DELETE")

        let (data, response) = try await httpClient.data(for: request)
        try checkResponse(response, data: data)
    }

    // MARK: - Private helpers

    /// Strips read-only and sub-resource fields before sending to the API.
    /// The server rejects requests with ports/endpoints/status in the body.
    private static func tunnelForRequest(_ tunnel: Tunnel) -> Tunnel {
        Tunnel(
            clusterId: tunnel.clusterId,
            tunnelId: tunnel.tunnelId,
            name: tunnel.name,
            description: tunnel.description,
            labels: tunnel.labels,
            domain: tunnel.domain
        )
    }

    private static let adjectives = [
        "fun", "happy", "interesting", "neat", "peaceful", "puzzled", "kind",
        "joyful", "new", "giant", "sneaky", "quick", "majestic", "jolly",
        "fancy", "tidy", "swift", "silent", "amusing", "spiffy",
    ]
    private static let nouns = [
        "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat",
        "shoe", "plane", "chair", "book", "ocean", "lake", "river", "horse",
    ]
    private static let idChars = Array("bcdfghjklmnpqrstvwxz0123456789")

    /// Generates a tunnel ID in the same format as the official SDKs.
    /// Format: `{adjective}-{noun}-{7 random chars}` (e.g., "swift-lake-bcd3f7k")
    static func generateTunnelId() -> String {
        // The static arrays are non-empty, but `randomElement()` returns
        // Optional. Fall back to deterministic values rather than crashing if
        // the arrays were ever emptied.
        let adj = adjectives.randomElement() ?? "tunnel"
        let noun = nouns.randomElement() ?? "id"
        let suffix = String((0..<7).compactMap { _ in idChars.randomElement() })
        return "\(adj)-\(noun)-\(suffix)"
    }

    private func buildURL(clusterId: String?, path: String, queryItems: [URLQueryItem]) throws -> URL {
        guard let serviceURL = URL(string: serviceProperties.serviceUri),
              let host = serviceURL.host() else {
            throw TunnelManagementError.invalidRequest("Invalid service URI: \(serviceProperties.serviceUri)")
        }

        var baseHost = host
        if let clusterId, !clusterId.isEmpty {
            baseHost = "\(clusterId).\(baseHost)".replacingOccurrences(of: "global.", with: "")
        }

        var components = URLComponents()
        components.scheme = "https"
        components.host = baseHost
        components.path = path
        components.queryItems = queryItems

        guard let url = components.url else {
            throw TunnelManagementError.invalidRequest("Failed to build URL for path: \(path)")
        }
        return url
    }

    private static let knownAuthSchemes = ["github", "aad", "bearer", "tunnel", "tunnelplan"]

    private func buildRequest(url: URL, method: String) -> URLRequest {
        var request = URLRequest(url: url)
        request.httpMethod = method

        // If token already has a known scheme prefix, use as-is; otherwise assume GitHub.
        let lowerToken = accessToken.lowercased()
        let hasScheme = Self.knownAuthSchemes.contains { lowerToken.hasPrefix($0 + " ") }
        let authHeader = hasScheme ? accessToken : "github \(accessToken)"
        request.setValue(authHeader, forHTTPHeaderField: "Authorization")

        request.setValue(userAgent, forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        return request
    }

    private func checkResponse(_ response: URLResponse, data: Data) throws {
        guard let httpResponse = response as? HTTPURLResponse else { return }
        guard (200..<300).contains(httpResponse.statusCode) else {
            let message = String(data: data, encoding: .utf8) ?? "Unknown error"
            // Include response headers in error for debugging auth issues
            var details = message
            if let wwwAuth = httpResponse.value(forHTTPHeaderField: "WWW-Authenticate") {
                details += " | WWW-Authenticate: \(wwwAuth)"
            }
            if let requestUrl = httpResponse.url?.absoluteString {
                details += " | URL: \(requestUrl)"
            }
            throw TunnelManagementError.httpError(
                statusCode: httpResponse.statusCode,
                message: details
            )
        }
    }
}
