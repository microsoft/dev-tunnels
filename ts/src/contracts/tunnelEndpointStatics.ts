// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelEndpoint as ITunnelEndpoint, portToken } from './tunnelEndpoint';

/**
 * Gets a URI where a web client can connect to a tunnel port.
 *
 * Requests to the URI may result in HTTP 307 redirections, so the client may need to
 * follow the redirection in order to connect to the port.
 *
 * If the port is not currently shared via the tunnel, or if a host is not currently
 * connected to the tunnel, then requests to the port URI may result in a 502 Bad Gateway
 * response.
 *
 * @param endpoint The tunnel endpoint containing connection information.
 * @param portNumber The port number to connect to; the port is assumed to be
 * separately shared by a tunnel host.
 * @returns URI for the requested port, or `undefined` if the endpoint does not support
 * web client connections.
 */
export function getPortUri(endpoint: ITunnelEndpoint, portNumber?: number): string | undefined {
    if (!endpoint) {
        throw new TypeError('A tunnel endpoint is required.');
    }

    if (typeof portNumber !== 'number' || !endpoint.portUriFormat) {
        return undefined;
    }

    return endpoint.portUriFormat.replace(portToken, portNumber.toString());
}

export function getPortSshCommand(
    endpoint: ITunnelEndpoint,
    portNumber?: number,
): string | undefined {
    if (!endpoint) {
        throw new TypeError('A tunnel endpoint is required.');
    }

    if (typeof portNumber !== 'number' || !endpoint.portSshCommandFormat) {
        return undefined;
    }

    return endpoint.portSshCommandFormat.replace(portToken, portNumber.toString());
}
