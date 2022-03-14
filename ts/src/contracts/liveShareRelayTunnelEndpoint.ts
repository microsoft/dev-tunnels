import { TunnelConnectionMode } from './tunnelConnectionMode';
import { TunnelEndpoint } from './tunnelEndpoint';

/**
 * Parameters for connecting to a tunnel via a Live Share Azure Relay.
 */
export interface LiveShareRelayTunnelEndpoint extends TunnelEndpoint {
    /**
     * Gets or sets the Live Share workspace ID.
     */
    workspaceId?: string;

    /**
     * Gets or sets the Azure Relay URI.
     */
    relayUri?: string;

    /**
     * Gets or sets a SAS token that allows hosts to listen on the Azure Relay endpoint.
     */
    relayHostSasToken?: string;

    /**
     * Gets or sets a SAS token that allows clients to connect to the Azure Relay endpoint.
     */
    relayClientSasToken?: string;

    /**
     * Gets or sets an array of public keys, which can be used by clients to authenticate the host.
     */
    hostKeys?: string[];
}
