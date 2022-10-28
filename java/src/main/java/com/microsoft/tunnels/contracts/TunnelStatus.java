// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelStatus.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Date;

/**
 * Data contract for {@link Tunnel} status.
 */
public class TunnelStatus {
    /**
     * Gets or sets the current value and limit for the number of ports on the tunnel.
     */
    @Expose
    public ResourceStatus portCount;

    /**
     * Gets or sets the current value and limit for the number of hosts currently
     * accepting connections to the tunnel.
     *
     * This is typically 0 or 1, but may be more than 1 if the tunnel options allow
     * multiple hosts.
     */
    @Expose
    public ResourceStatus hostConnectionCount;

    /**
     * Gets or sets the UTC time when a host was last accepting connections to the tunnel,
     * or null if a host has never connected.
     */
    @Expose
    public Date lastHostConnectionTime;

    /**
     * Gets or sets the current value and limit for the number of clients connected to the
     * tunnel.
     *
     * This counts non-port-specific client connections, which is SDK and SSH clients. See
     * {@link TunnelPortStatus} for status of per-port client connections.
     */
    @Expose
    public ResourceStatus clientConnectionCount;

    /**
     * Gets or sets the UTC time when a client last connected to the tunnel, or null if a
     * client has never connected.
     *
     * This reports times for non-port-specific client connections, which is SDK client
     * and SSH clients. See {@link TunnelPortStatus} for per-port client connections.
     */
    @Expose
    public Date lastClientConnectionTime;

    /**
     * Gets or sets the current value and limit for the rate of client connections to the
     * tunnel.
     *
     * This counts non-port-specific client connections, which is SDK client and SSH
     * clients. See {@link TunnelPortStatus} for status of per-port client connections.
     */
    @Expose
    public RateStatus clientConnectionRate;

    /**
     * Gets or sets the current value and limit for the rate of bytes being received by
     * the tunnel host and uploaded by tunnel clients.
     *
     * All types of tunnel and port connections, from potentially multiple clients, can
     * contribute to this rate. The reported rate may differ slightly from the rate
     * measurable by applications, due to protocol overhead. Data rate status reporting is
     * delayed by a few seconds, so this value is a snapshot of the data transfer rate
     * from a few seconds earlier.
     */
    @Expose
    public RateStatus uploadRate;

    /**
     * Gets or sets the current value and limit for the rate of bytes being sent by the
     * tunnel host and downloaded by tunnel clients.
     *
     * All types of tunnel and port connections, from potentially multiple clients, can
     * contribute to this rate. The reported rate may differ slightly from the rate
     * measurable by applications, due to protocol overhead. Data rate status reporting is
     * delayed by a few seconds, so this value is a snapshot of the data transfer rate
     * from a few seconds earlier.
     */
    @Expose
    public RateStatus downloadRate;

    /**
     * Gets or sets the total number of bytes received by the tunnel host and uploaded by
     * tunnel clients, over the lifetime of the tunnel.
     *
     * All types of tunnel and port connections, from potentially multiple clients, can
     * contribute to this total. The reported value may differ slightly from the value
     * measurable by applications, due to protocol overhead. Data transfer status
     * reporting is delayed by a few seconds.
     */
    @Expose
    public long uploadTotal;

    /**
     * Gets or sets the total number of bytes sent by the tunnel host and downloaded by
     * tunnel clients, over the lifetime of the tunnel.
     *
     * All types of tunnel and port connections, from potentially multiple clients, can
     * contribute to this total. The reported value may differ slightly from the value
     * measurable by applications, due to protocol overhead. Data transfer status
     * reporting is delayed by a few seconds.
     */
    @Expose
    public long downloadTotal;

    /**
     * Gets or sets the current value and limit for the rate of management API read
     * operations  for the tunnel or tunnel ports.
     */
    @Expose
    public RateStatus apiReadRate;

    /**
     * Gets or sets the current value and limit for the rate of management API update
     * operations for the tunnel or tunnel ports.
     */
    @Expose
    public RateStatus apiUpdateRate;
}
