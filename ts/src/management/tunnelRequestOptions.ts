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
     * Gets or sets an optional list of scopes that should be authorized when
     * retrieving a tunnel or tunnel port object.
     */
    scopes?: string[];

    /**
     * Gets or sets an optional list of token scopes that
     * are requested when retrieving a tunnel or tunnel port object.
     */
    tokenScopes?: string[];
}
