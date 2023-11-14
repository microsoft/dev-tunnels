// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelHeaderNames.cs
/* eslint-disable */

/**
 * Header names for http requests that Tunnel Service can handle
 */
export enum TunnelHeaderNames {
    /**
     * Additional authorization header that can be passed to tunnel web forwarding to
     * authenticate and authorize the client. The format of the value is the same as
     * Authorization header that is sent to the Tunnel service by the tunnel SDK.
     * Supported schemes: "tunnel" with the tunnel access JWT good for 'Connect' scope.
     */
    XTunnelAuthorization = 'X-Tunnel-Authorization',

    /**
     * Request ID header that nginx ingress controller adds to all requests if it's not
     * there.
     */
    XRequestID = 'X-Request-ID',

    /**
     * Github Ssh public key which can be used to validate if it belongs to tunnel's
     * owner.
     */
    XGithubSshKey = 'X-Github-Ssh-Key',

    /**
     * Header that will skip the antiphishing page when connection to a tunnel through web
     * forwarding.
     */
    XTunnelSkipAntiPhishingPage = 'X-Tunnel-Skip-AntiPhishing-Page',
}
