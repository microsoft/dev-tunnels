// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelConnectionMode.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

/**
 * Specifies the connection protocol / implementation for a tunnel.
 *
 * Depending on the connection mode, hosts or clients might need to use different
 * authentication and connection protocols.
 */
public enum TunnelConnectionMode {
    /**
     * Connect directly to the host over the local network.
     *
     * While it's technically not "tunneling", this mode may be combined with others to
     * enable choosing the most efficient connection mode available.
     */
    @SerializedName("LocalNetwork")
    LocalNetwork,

    /**
     * Use the tunnel service's integrated relay function.
     */
    @SerializedName("TunnelRelay")
    TunnelRelay,
}
