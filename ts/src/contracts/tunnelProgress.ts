// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs
/* eslint-disable */

/**
 * Specifies the tunnel progress events that are reported.
 */
export enum TunnelProgress {
    /**
     * Starting refresh ports.
     */
    StartingRefreshPorts = 'StartingRefreshPorts',

    /**
     * Completed refresh ports.
     */
    CompletedRefreshPorts = 'CompletedRefreshPorts',

    /**
     * Starting request uri for a tunnel service request.
     */
    StartingRequestUri = 'StartingRequestUri',

    /**
     * Starting request configuration for a tunnel service request.
     */
    StartingRequestConfig = 'StartingRequestConfig',

    /**
     * Starting to send tunnel service request.
     */
    StartingSendTunnelRequest = 'StartingSendTunnelRequest',

    /**
     * Completed sending a tunnel service request.
     */
    CompletedSendTunnelRequest = 'CompletedSendTunnelRequest',

    /**
     * Starting create tunnel port.
     */
    StartingCreateTunnelPort = 'StartingCreateTunnelPort',

    /**
     * Completed create tunnel port.
     */
    CompletedCreateTunnelPort = 'CompletedCreateTunnelPort',

    /**
     * Starting get tunnel port.
     */
    StartingGetTunnelPort = 'StartingGetTunnelPort',

    /**
     * Completed get tunnel port.
     */
    CompletedGetTunnelPort = 'CompletedGetTunnelPort',
}
