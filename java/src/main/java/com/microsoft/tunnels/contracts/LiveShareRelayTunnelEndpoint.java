// Generated from ../../../../../../../../cs/src/Contracts/LiveShareRelayTunnelEndpoint.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Parameters for connecting to a tunnel via a Live Share Azure Relay.
 */
public class LiveShareRelayTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets the Live Share workspace ID.
     */
    @Expose
    public String workspaceId;

    /**
     * Gets or sets the Azure Relay URI.
     */
    @Expose
    public String relayUri;

    /**
     * Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
     */
    @Expose
    public String relayHostSasToken;

    /**
     * Gets or sets a SAS token that allows clients to connect to the Azure Relay
     * endpoint.
     */
    @Expose
    public String relayClientSasToken;
}
