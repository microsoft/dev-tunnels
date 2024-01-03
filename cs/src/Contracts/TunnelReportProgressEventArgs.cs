// <copyright file="TunnelReportProgressEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Specifies the tunnel progress events that are reported.
    /// </summary>
    public enum TunnelProgress
    {
        /// <summary>
        /// Starting refresh ports.
        /// </summary>
        StartingRefreshPorts,

        /// <summary>
        /// Completed refresh ports.
        /// </summary>
        CompletedRefreshPorts,

        /// <summary>
        /// Starting request uri for a tunnel service request.
        /// </summary>
        StartingRequestUri,

        /// <summary>
        /// Starting request configuration for a tunnel service request.
        /// </summary>
        StartingRequestConfig,

        /// <summary>
        /// Starting to send tunnel service request.
        /// </summary>
        StartingSendTunnelRequest,

        /// <summary>
        /// Completed sending a tunnel service request.
        /// </summary>
        CompletedSendTunnelRequest,

        /// <summary>
        /// Starting create tunnel port.
        /// </summary>
        StartingCreateTunnelPort,

        /// <summary>
        /// Completed create tunnel port.
        /// </summary>
        CompletedCreateTunnelPort,

        /// <summary>
        /// Starting get tunnel port.
        /// </summary>
        StartingGetTunnelPort,

        /// <summary>
        /// Completed get tunnel port.
        /// </summary>
        CompletedGetTunnelPort,
    }

    /// <summary>
    /// Event args for the tunnel report progress event.
    /// </summary>
    public class TunnelReportProgressEventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="TunnelReportProgressEventArgs"/> class.
        /// </summary>
        public TunnelReportProgressEventArgs(string progress, int? sessionNumber = null)
        {
            Progress = progress;
            SessionNumber = sessionNumber;
        }

        /// <summary>
        /// Specifies the progress event that is being reported. See <see cref="TunnelProgress"/> and
        /// Ssh.Progress for a description of the different progress events that can be reported.
        /// </summary>
        public string Progress { get; }

        /// <summary>
        /// The session number associated with an SSH session progress event.
        /// </summary>
        public int? SessionNumber { get; }
    }
}
