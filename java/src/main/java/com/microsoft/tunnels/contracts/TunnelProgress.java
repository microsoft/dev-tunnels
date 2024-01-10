// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.SerializedName;

/**
 * Specifies the tunnel progress events that are reported.
 */
public enum TunnelProgress {
    /**
     * Starting refresh ports.
     */
    @SerializedName("StartingRefreshPorts")
    StartingRefreshPorts,

    /**
     * Completed refresh ports.
     */
    @SerializedName("CompletedRefreshPorts")
    CompletedRefreshPorts,

    /**
     * Starting request uri for a tunnel service request.
     */
    @SerializedName("StartingRequestUri")
    StartingRequestUri,

    /**
     * Starting request configuration for a tunnel service request.
     */
    @SerializedName("StartingRequestConfig")
    StartingRequestConfig,

    /**
     * Starting to send tunnel service request.
     */
    @SerializedName("StartingSendTunnelRequest")
    StartingSendTunnelRequest,

    /**
     * Completed sending a tunnel service request.
     */
    @SerializedName("CompletedSendTunnelRequest")
    CompletedSendTunnelRequest,

    /**
     * Starting create tunnel port.
     */
    @SerializedName("StartingCreateTunnelPort")
    StartingCreateTunnelPort,

    /**
     * Completed create tunnel port.
     */
    @SerializedName("CompletedCreateTunnelPort")
    CompletedCreateTunnelPort,

    /**
     * Starting get tunnel port.
     */
    @SerializedName("StartingGetTunnelPort")
    StartingGetTunnelPort,

    /**
     * Completed get tunnel port.
     */
    @SerializedName("CompletedGetTunnelPort")
    CompletedGetTunnelPort,
}
