// <copyright file="LiveShareRelayTunnelEndpoint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Parameters for connecting to a tunnel via a Live Share Azure Relay.
/// </summary>
public class LiveShareRelayTunnelEndpoint : TunnelEndpoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveShareRelayTunnelEndpoint"/> class.
    /// </summary>
    public LiveShareRelayTunnelEndpoint()
    {
        ConnectionMode = TunnelConnectionMode.LiveShareRelay;
    }

    /// <summary>
    /// Gets or sets the Live Share workspace ID.
    /// </summary>
    public string WorkspaceId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Azure Relay URI.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelayUri { get; set; }

    /// <summary>
    /// Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelayHostSasToken { get; set; }

    /// <summary>
    /// Gets or sets a SAS token that allows clients to connect to the Azure Relay endpoint.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelayClientSasToken { get; set; }
}
