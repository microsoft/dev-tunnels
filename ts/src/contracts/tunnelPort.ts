// Generated from ../../../cs/src/Contracts/TunnelPort.cs
/* eslint-disable */

import { TunnelAccessControl } from './tunnelAccessControl';
import { TunnelOptions } from './tunnelOptions';
import { TunnelPortStatus } from './tunnelPortStatus';

/**
 * Data contract for tunnel port objects managed through the tunnel service REST API.
 */
export interface TunnelPort {
    /**
     * Gets or sets the ID of the cluster the tunnel was created in.
     */
    clusterId?: string;

    /**
     * Gets or sets the generated ID of the tunnel, unique within the cluster.
     */
    tunnelId?: string;

    /**
     * Gets or sets the IP port number of the tunnel port.
     */
    portNumber: number;

    /**
     * Gets or sets the protocol of the tunnel port.
     *
     * Should be one of the string constants from {@link TunnelProtocol}.
     */
    protocol?: string;

    /**
     * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
     *
     * Unlike the tokens in {@link Tunnel.accessTokens}, these tokens are restricted to
     * the individual port.
     */
    accessTokens?: { [scope: string]: string };

    /**
     * Gets or sets access control settings for the tunnel port.
     *
     * See {@link TunnelAccessControl} documentation for details about the access control
     * model.
     */
    accessControl?: TunnelAccessControl;

    /**
     * Gets or sets options for the tunnel port.
     */
    options?: TunnelOptions;

    /**
     * Gets or sets current connection status of the tunnel port.
     */
    status?: TunnelPortStatus;
}
