// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

/**
 * Event args for the tunnel report progress event.
 */
public class TunnelReportProgressEventArgs {
    TunnelReportProgressEventArgs (String progress, int sessionNumber) {
        this.progress = progress;
        this.sessionNumber = sessionNumber;
    }

    /**
     * Specifies the progress event that is being reported. See {@link TunnelProgress} and
     * Ssh.Progress for a description of the different progress events that can be
     * reported.
     */
    @Expose
    public final String progress;

    /**
     * The session number associated with an SSH session progress event.
     */
    @Expose
    public final int sessionNumber;
}
