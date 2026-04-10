import Foundation

/// Errors from the tunnel management API.
public enum TunnelManagementError: Error, Equatable, LocalizedError {
    /// HTTP error with status code and message body.
    case httpError(statusCode: Int, message: String)

    /// Failed to decode the response body.
    case decodingError(String)

    /// Invalid request parameters.
    case invalidRequest(String)

    public var errorDescription: String? {
        switch self {
        case .httpError(let statusCode, let message):
            return "HTTP \(statusCode): \(message)"
        case .decodingError(let message):
            return "Decoding error: \(message)"
        case .invalidRequest(let message):
            return "Invalid request: \(message)"
        }
    }
}
