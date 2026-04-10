/// Service properties for Dev Tunnels environments.
public struct TunnelServiceProperties: Sendable {
    /// Base URI for the tunnel service.
    public let serviceUri: String

    /// GitHub OAuth app client ID for device code auth.
    public let gitHubAppClientId: String

    /// API version for requests.
    public let apiVersion: String

    public init(
        serviceUri: String,
        gitHubAppClientId: String,
        apiVersion: String = "2023-09-27-preview"
    ) {
        self.serviceUri = serviceUri
        self.gitHubAppClientId = gitHubAppClientId
        self.apiVersion = apiVersion
    }
}

extension TunnelServiceProperties {
    /// Production service properties.
    public static let production = TunnelServiceProperties(
        serviceUri: "https://global.rel.tunnels.api.visualstudio.com",
        gitHubAppClientId: "Iv1.e7b89e013f801f03"
    )
}
