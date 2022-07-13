// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Options for tunnel service requests.
 */
export interface TunnelRequestOptions {
    /**
     * Gets or sets an access token for the request.
     *
     * Note this should not be a _user_ access token (such as AAD or GitHub); use the
     * callback parameter to the `TunnelManagementHttpClient` constructor to
     * supply user access tokens.
     */
    accessToken?: string;

    /**
     * Gets or sets additional headers to be included in the request.
     */
    additionalHeaders?: { [header: string]: string };

    /**
     * Gets or sets a value that indicates whether HTTP redirect responses will be
     * automatically followed.
     *
     * The default is true. If false, a redirect response will cause an error to be thrown,
     * with redirect target URI available at `error.response.headers.location`.
     *
     * The tunnel service often redirects requests to the "home" cluster of the requested
     * tunnel, when necessary to fulfill the request.
     */
    followRedirects?: boolean;

    /**
     * Gets or sets a flag that requests tunnel ports when retrieving a tunnel object.
     */
    includePorts?: boolean;

    /**
     * Gets or sets an optional list of tags to filter the requested tunnels or ports.
     *
     * Requested tags are compared to the `Tunnel.tags` or `TunnelPort.tags` when calling
     * `TunnelManagementClient.listTunnels` or `TunnelManagementClient.listTunnelPorts`
     * respectively. By default, an item is included if ANY tag matches; set `requireAllTags`
     * to match ALL tags instead.
     */
    tags?: string[];

    /*
     * Gets or sets a flag that indicates whether listed items must match all tags
     * specified in `tags`. If false, an item is included if any tag matches.
     */
    requireAllTags?: boolean;

    /**
     * Gets or sets an optional list of scopes that should be authorized when
     * retrieving a tunnel or tunnel port object.
     */
    scopes?: string[];

    /**
     * Gets or sets an optional list of token scopes that
     * are requested when retrieving a tunnel or tunnel port object.
     */
    tokenScopes?: string[];

    /**
     * If true on a create or update request then upon a name conflict, attempt to rename the
     * existing tunnel to null and give the name to the tunnel from the request.
     */
    forceRename?: boolean;
}
