// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs
/* eslint-disable */

/**
 * Event args for the tunnel report progress event.
 */
export interface TunnelReportProgressEventArgs {
    /**
     * Specifies the progress event that is being reported. See {@link TunnelProgress} and
     * Ssh.Progress for a description of the different progress events that can be
     * reported.
     */
    progress: string;

    /**
     * The session number associated with an SSH session progress event.
     */
    sessionNumber?: number;
}
