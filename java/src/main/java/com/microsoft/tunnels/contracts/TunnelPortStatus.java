// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelPortStatus.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Date;

/**
 * Data contract for {@link TunnelPort} status.
 */
public class TunnelPortStatus {
    /**
     * Gets or sets the number of clients currently connected to the port.
     *
     * The client connection count does not include the host. (See the {@link
     * TunnelStatus#hostConnectionCount} property for host connection status. Hosts always
     * listen for incoming connections on all tunnel ports simultaneously.)
     */
    @Expose
    public int clientConnectionCount;

    /**
     * Gets or sets the UTC date time when a client was last connected to the port.
     */
    @Expose
    public Date lastClientConnectionTime;
}
