// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelEndpoint.cs
/* eslint-disable */

import { TunnelConnectionMode } from './tunnelConnectionMode';

/**
 * Base class for tunnel connection parameters.
 *
 * A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
 * There is a subclass for each connection mode, each having different connection
 * parameters. A tunnel may have multiple endpoints for one host (or multiple hosts), and
 * clients can select their preferred endpoint(s) from those depending on network
 * environment or client capabilities.
 */
export interface TunnelEndpoint {
    /**
     * Gets or sets the connection mode of the endpoint.
     *
     * This property is required when creating or updating an endpoint.  The subclass type
     * is also an indication of the connection mode, but this property is necessary to
     * determine the subclass type when deserializing.
     */
    connectionMode: TunnelConnectionMode;

    /**
     * Gets or sets the ID of the host that is listening on this endpoint.
     *
     * This property is required when creating or updating an endpoint.  If the host
     * supports multiple connection modes, the host's ID is the same for all the endpoints
     * it supports. However different hosts may simultaneously accept connections at
     * different endpoints for the same tunnel, if enabled in tunnel options.
     */
    hostId: string;

    /**
     * Gets or sets an array of public keys, which can be used by clients to authenticate
     * the host.
     */
    hostPublicKeys?: string[];

    /**
     * Gets or sets a string used to format URIs where a web client can connect to ports
     * of the tunnel. The string includes a {@link TunnelEndpoint.portToken} that must be
     * replaced with the actual port number.
     */
    portUriFormat?: string;

    /**
     * Gets or sets a string used to format ssh command where ssh client can connect to
     * shared ssh port of the tunnel. The string includes a {@link
     * TunnelEndpoint.portToken} that must be replaced with the actual port number.
     */
    portSshCommandFormat?: string;
}

/**
 * Token included in {@link TunnelEndpoint.portUriFormat} and {@link
 * TunnelEndpoint.portSshCommandFormat} that is to be replaced by a specified port number.
 */
export const portToken = '{port}';

// Import static members from a non-generated file,
// and re-export them as an object with the same name as the interface.
import {
    getPortUri,
    getPortSshCommand,
} from './tunnelEndpointStatics';

export const TunnelEndpoint = {
    portToken,
    getPortUri,
    getPortSshCommand,
};
