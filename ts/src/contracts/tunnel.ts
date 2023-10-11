// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/Tunnel.cs
/* eslint-disable */

import { TunnelAccessControl } from './tunnelAccessControl';
import { TunnelEndpoint } from './tunnelEndpoint';
import { TunnelOptions } from './tunnelOptions';
import { TunnelPort } from './tunnelPort';
import { TunnelStatus } from './tunnelStatus';

/**
 * Data contract for tunnel objects managed through the tunnel service REST API.
 */
export interface Tunnel {
    /**
     * Gets or sets the ID of the cluster the tunnel was created in.
     */
    clusterId?: string;

    /**
     * Gets or sets the generated ID of the tunnel, unique within the cluster.
     */
    tunnelId?: string;

    /**
     * Gets or sets the optional short name (alias) of the tunnel.
     *
     * The name must be globally unique within the parent domain, and must be a valid
     * subdomain.
     */
    name?: string;

    /**
     * Gets or sets the description of the tunnel.
     */
    description?: string;

    /**
     * Gets or sets the labels of the tunnel.
     */
    labels?: string[];

    /**
     * Gets or sets the optional parent domain of the tunnel, if it is not using the
     * default parent domain.
     */
    domain?: string;

    /**
     * Gets or sets a dictionary mapping from scopes to tunnel access tokens.
     */
    accessTokens?: { [scope: string]: string };

    /**
     * Gets or sets access control settings for the tunnel.
     *
     * See {@link TunnelAccessControl} documentation for details about the access control
     * model.
     */
    accessControl?: TunnelAccessControl;

    /**
     * Gets or sets default options for the tunnel.
     */
    options?: TunnelOptions;

    /**
     * Gets or sets current connection status of the tunnel.
     */
    status?: TunnelStatus;

    /**
     * Gets or sets an array of endpoints where hosts are currently accepting client
     * connections to the tunnel.
     */
    endpoints?: TunnelEndpoint[];

    /**
     * Gets or sets a list of ports in the tunnel.
     *
     * This optional property enables getting info about all ports in a tunnel at the same
     * time as getting tunnel info, or creating one or more ports at the same time as
     * creating a tunnel. It is omitted when listing (multiple) tunnels, or when updating
     * tunnel properties. (For the latter, use APIs to create/update/delete individual
     * ports instead.)
     */
    ports?: TunnelPort[];

    /**
     * Gets or sets the time in UTC of tunnel creation.
     */
    created?: Date;

    /**
     * Gets or the time the tunnel will be deleted if it is not used or updated.
     */
    expiration?: Date;

    /**
     * Gets or the custom amount of time the tunnel will be valid if it is not used or
     * updated in seconds.
     */
    customExpiration?: number;
}
