// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

package tunnels

// Event args for the tunnel report progress event.
type TunnelReportProgressEventArgs struct {
	// Specifies the progress event that is being reported. See `TunnelProgress` and
	// Ssh.Progress for a description of the different progress events that can be reported.
	Progress      string `json:"progress"`

	// The session number associated with an SSH session progress event.
	SessionNumber int32 `json:"sessionNumber"`
}
