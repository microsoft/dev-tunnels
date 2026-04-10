import Foundation

/// Response from starting a GitHub device code auth flow.
public struct DeviceCodeResponse: Codable, Equatable, Sendable {
    /// The device code used for polling (not shown to user).
    public let deviceCode: String

    /// The code the user enters at the verification URI.
    public let userCode: String

    /// The URL the user visits to enter the code.
    public let verificationUri: String

    /// Seconds until the device code expires.
    public let expiresIn: Int

    /// Minimum seconds between poll requests.
    public let interval: Int

    private enum CodingKeys: String, CodingKey {
        case deviceCode = "device_code"
        case userCode = "user_code"
        case verificationUri = "verification_uri"
        case expiresIn = "expires_in"
        case interval
    }
}

/// Result of polling for device code authorization completion.
public enum DeviceCodePollResult: Equatable, Sendable {
    /// User completed authorization. Contains the GitHub access token.
    case accessToken(String)
    /// Authorization is still pending — poll again after `interval` seconds.
    case pending
    /// The device code expired. Start a new flow.
    case expired
    /// The flow was denied or encountered an error.
    case error(String)
}

/// GitHub device code OAuth flow for Dev Tunnels authentication.
///
/// Usage:
/// ```swift
/// let response = try await DeviceCodeAuth.start()
/// print("Visit \(response.verificationUri) and enter: \(response.userCode)")
///
/// while true {
///     try await Task.sleep(for: .seconds(response.interval))
///     let result = try await DeviceCodeAuth.poll(deviceCode: response.deviceCode)
///     switch result {
///     case .accessToken(let token): // Done!
///     case .pending: continue
///     case .expired: // Restart
///     case .error(let msg): // Handle
///     }
/// }
/// ```
public struct DeviceCodeAuth: Sendable {
    private let httpClient: any HTTPClient
    private let serviceProperties: TunnelServiceProperties

    public init(
        serviceProperties: TunnelServiceProperties = .production,
        httpClient: any HTTPClient = URLSession.shared
    ) {
        self.httpClient = httpClient
        self.serviceProperties = serviceProperties
    }

    /// Start a device code auth flow with GitHub.
    ///
    /// Returns a `DeviceCodeResponse` with the `userCode` to display and `verificationUri`
    /// for the user to visit. Then call `poll(deviceCode:)` with the `deviceCode`.
    public func start() async throws -> DeviceCodeResponse {
        let url = URL(string: "https://github.com/login/device/code")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let body = "client_id=\(serviceProperties.gitHubAppClientId)&scope=user:email read:org"
        request.httpBody = body.data(using: .utf8)
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")

        let (data, response) = try await httpClient.data(for: request)

        if let httpResponse = response as? HTTPURLResponse,
           !(200..<300).contains(httpResponse.statusCode) {
            let message = String(data: data, encoding: .utf8) ?? "Unknown error"
            throw TunnelManagementError.httpError(
                statusCode: httpResponse.statusCode,
                message: message
            )
        }

        do {
            return try JSONDecoder().decode(DeviceCodeResponse.self, from: data)
        } catch {
            throw TunnelManagementError.decodingError("Failed to decode device code response: \(error)")
        }
    }

    /// Poll GitHub for device code authorization completion.
    ///
    /// Call repeatedly with the `deviceCode` from `start()`,
    /// waiting at least `interval` seconds between calls.
    public func poll(deviceCode: String) async throws -> DeviceCodePollResult {
        let url = URL(string: "https://github.com/login/oauth/access_token")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let body = [
            "client_id=\(serviceProperties.gitHubAppClientId)",
            "device_code=\(deviceCode)",
            "grant_type=urn:ietf:params:oauth:grant-type:device_code",
        ].joined(separator: "&")
        request.httpBody = body.data(using: .utf8)
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")

        let (data, response) = try await httpClient.data(for: request)

        if let httpResponse = response as? HTTPURLResponse,
           !(200..<300).contains(httpResponse.statusCode) {
            let message = String(data: data, encoding: .utf8) ?? "Unknown error"
            throw TunnelManagementError.httpError(
                statusCode: httpResponse.statusCode,
                message: message
            )
        }

        let tokenResponse = try JSONDecoder().decode(GitHubTokenResponse.self, from: data)

        if let token = tokenResponse.accessToken {
            return .accessToken(token)
        }

        switch tokenResponse.error {
        case "authorization_pending", "slow_down":
            return .pending
        case "expired_token":
            return .expired
        default:
            let message = tokenResponse.errorDescription
                ?? tokenResponse.error
                ?? "Unknown error"
            return .error(message)
        }
    }
}

/// Internal response type for GitHub OAuth token endpoint.
struct GitHubTokenResponse: Codable {
    let accessToken: String?
    let error: String?
    let errorDescription: String?

    private enum CodingKeys: String, CodingKey {
        case accessToken = "access_token"
        case error
        case errorDescription = "error_description"
    }
}
