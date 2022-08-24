namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Tunnel service cluster details.
/// </summary>
public class ClusterDetails {
    /// <summary>
    /// A cluster identifier based on its region.
    /// </summary>
    public string? ClusterId { get; set; }
    /// <summary>
    /// The cluster DNS host.
    /// </summary>
    public string? Host { get; set; }
}