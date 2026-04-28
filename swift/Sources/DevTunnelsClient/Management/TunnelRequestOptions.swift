import Foundation

/// Options for tunnel API requests.
public struct TunnelRequestOptions: Sendable {
    /// Include ports in the response.
    public var includePorts: Bool

    /// Token scopes to request (e.g., ["connect"]).
    public var tokenScopes: [String]

    public init(
        includePorts: Bool = false,
        tokenScopes: [String] = []
    ) {
        self.includePorts = includePorts
        self.tokenScopes = tokenScopes
    }

    /// Converts options to URL query items.
    func queryItems() -> [URLQueryItem] {
        var items = [URLQueryItem]()
        if includePorts {
            items.append(URLQueryItem(name: "includePorts", value: "true"))
        }
        for scope in tokenScopes {
            items.append(URLQueryItem(name: "tokenScopes", value: scope))
        }
        return items
    }
}
