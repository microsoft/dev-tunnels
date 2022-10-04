namespace Microsoft.DevTunnels.Contracts;

/// <summary>
/// Details of a tunneling service cluster. Each cluster represents an instance of the
/// tunneling service running in a particular Azure region. New tunnels are created in
/// the current region unless otherwise specified.
/// </summary>
public class ClusterDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClusterDetails"/> class.
    /// </summary>
    public ClusterDetails(
        string clusterId,
        string uri,
        string azureLocation)
    {
        ClusterId = clusterId;
        Uri = uri;
        AzureLocation = azureLocation;
    }
    /// <summary>
    /// A cluster identifier based on its region.
    /// </summary>
    public string ClusterId { get; }
    /// <summary>
    /// The URI of the service cluster.
    /// </summary>
    public string Uri { get; }
    /// <summary>
    /// The Azure location of the cluster.
    /// </summary>
    public string AzureLocation { get; }
}