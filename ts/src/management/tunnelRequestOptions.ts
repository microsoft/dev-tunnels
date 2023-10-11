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
     * Gets or sets additional query parameters to be included in the request.
     */
    additionalQueryParameters?: { [name: string]: string };

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
     *
     * Ports are excluded by default when retrieving a tunnel or when listing or searching
     * tunnels. This option enables including ports for all tunnels returned by a list or
     * search query.
     */
    includePorts?: boolean;

    /**
     * Gets or sets a flag that requests access control details when retrieving tunnels.
     *
     * Access control details are always included when retrieving a single tunnel,
     * but excluded by default when listing or searching tunnels. This option enables
     * including access controls for all tunnels returned by a list or search query.
     */
    includeAccessControl?: boolean;

    /**
     * Gets or sets an optional list of labels to filter the requested tunnels or ports.
     *
     * Requested labels are compared to the `Tunnel.labels` or `TunnelPort.labels` when calling
     * `TunnelManagementClient.listTunnels` or `TunnelManagementClient.listTunnelPorts`
     * respectively. By default, an item is included if ANY tag matches; set `requireAllLabels`
     * to match ALL labels instead.
     */
    labels?: string[];

    /*
     * Gets or sets a flag that indicates whether listed items must match all labels
     * specified in `labels`. If false, an item is included if any tag matches.
     */
    requireAllLabels?: boolean;

    /**
     * Gets or sets an optional list of token scopes that are requested when retrieving a
     * tunnel or tunnel port object.
     *
     * Each item in the array must be a single scope from `TunnelAccessScopes` or a space-
     * delimited combination of multiple scopes. The service issues an access token for
     * each scope or combination and returns the token(s) in the `Tunnel.accessTokens` or
     * `TunnelPort.accessTokens` dictionary. If the caller does not have permission to get
     * a token for one or more scopes then a token is not returned but the overall request
     * does not fail. Token properties including scopes and expiration may be checked using
     * `TunnelAccessTokenProperties`.
     */
    tokenScopes?: string[];

    /**
     * If true on a create or update request then upon a name conflict, attempt to rename the
     * existing tunnel to null and give the name to the tunnel from the request.
     */
    forceRename?: boolean;

    /**
     * Limits the number of tunnels returned when searching or listing tunnels.
     */
    limit?: number;
}
