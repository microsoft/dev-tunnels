// <copyright file="TunnelHeaderNames.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Header names for http requests that Tunnel Service can handle
/// </summary>
public static class TunnelHeaderNames
{
    /// <summary>
    /// Additional authorization header that can be passed to tunnel web forwarding to authenticate and authorize the client.
    /// The format of the value is the same as Authorization header that is sent to the Tunnel service by the tunnel SDK.
    /// Supported schemes:
    /// "tunnel" with the tunnel access JWT good for 'Connect' scope.
    /// </summary>
    public const string XTunnelAuthorization = "X-Tunnel-Authorization";

    /// <summary>
    /// Request ID header that nginx ingress controller adds to all requests if it's not there.
    /// </summary>
    public const string XRequestID = "X-Request-ID";

    /// <summary>
    /// Github Ssh public key which can be used to validate if it belongs to tunnel's owner.
    /// </summary>
    public const string XGithubSshKey = "X-Github-Ssh-Key";
}
