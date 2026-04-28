import Foundation

/// API response wrapper for list tunnels grouped by region.
struct TunnelListByRegionResponse: Codable {
    let value: [TunnelListByRegion]?
    let nextLink: String?
}

/// A group of tunnels in a region.
struct TunnelListByRegion: Codable {
    let regionName: String?
    let clusterId: String?
    let value: [Tunnel]?
}
