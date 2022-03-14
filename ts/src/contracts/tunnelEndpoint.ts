import { TunnelConnectionMode } from './tunnelConnectionMode';

/**
 * Base class for tunnel connection parameters.
 */
export interface TunnelEndpoint {
    /**
     * Gets or sets the connection mode of the endpoint.
     * This property is required when creating or updating an endpoint.
     */
    connectionMode?: TunnelConnectionMode;

    /**
     * Gets or sets the ID of the host that is listening on this endpoint.
     * This property is required when creating or updating an endpoint.
     */
    hostId?: string;

    /**
     * Gets or sets a string used to format URIs where a web client can connect to
     * ports of the tunnel. The string includes a `{port}` that must be
     * replaced with the actual port number.
     */
    portUriFormat?: string;
}

/**
 * Token included in `TunnelEndpoint.portUriFormat` that is to be replaced by a specified
 * port number.
 */
const portUriToken = '{port}';

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
export function getTunnelPortUri(
    endpoint: TunnelEndpoint,
    portNumber?: number,
): string | undefined {
    if (!endpoint) {
        throw new TypeError('A tunnel endpoint is required.');
    }

    if (typeof portNumber !== 'number' || !endpoint.portUriFormat) {
        return undefined;
    }

    return endpoint.portUriFormat.replace(portUriToken, portNumber.toString());
}
