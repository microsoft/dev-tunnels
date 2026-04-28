import Foundation

/// Abstraction over HTTP requests for testability.
/// URLSession conforms via extension; tests inject a mock.
public protocol HTTPClient: Sendable {
    func data(for request: URLRequest) async throws -> (Data, URLResponse)
}

extension URLSession: HTTPClient {}
