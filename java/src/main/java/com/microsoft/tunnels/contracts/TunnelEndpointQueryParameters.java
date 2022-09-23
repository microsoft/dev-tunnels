// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelEndpointQueryParameters.cs

package com.microsoft.tunnels.contracts;

/**
 * The query parameters that can be passed to the update/delete TunnelEndpoint api.
 */
public class TunnelEndpointQueryParameters {
    /**
     * Include ssh gateway public key in the TunnelEndpoint response. This will be set to
     * true by the SDK if the tunnel has ssh ports.
     */
    public static String includeSshGatewayPublicKey = "includesshGatewayPublicKey";
}
