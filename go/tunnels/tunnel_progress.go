// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelReportProgressEventArgs.cs

package tunnels

// Specifies the tunnel progress events that are reported.
type TunnelProgress []TunnelProgres
type TunnelProgres string

const (
	// Starting refresh ports.
	TunnelProgresStartingRefreshPorts       TunnelProgres = "StartingRefreshPorts"

	// Completed refresh ports.
	TunnelProgresCompletedRefreshPorts      TunnelProgres = "CompletedRefreshPorts"

	// Starting request uri for a tunnel service request.
	TunnelProgresStartingRequestUri         TunnelProgres = "StartingRequestUri"

	// Starting request configuration for a tunnel service request.
	TunnelProgresStartingRequestConfig      TunnelProgres = "StartingRequestConfig"

	// Starting to send tunnel service request.
	TunnelProgresStartingSendTunnelRequest  TunnelProgres = "StartingSendTunnelRequest"

	// Completed sending a tunnel service request.
	TunnelProgresCompletedSendTunnelRequest TunnelProgres = "CompletedSendTunnelRequest"

	// Starting create tunnel port.
	TunnelProgresStartingCreateTunnelPort   TunnelProgres = "StartingCreateTunnelPort"

	// Completed create tunnel port.
	TunnelProgresCompletedCreateTunnelPort  TunnelProgres = "CompletedCreateTunnelPort"

	// Starting get tunnel port.
	TunnelProgresStartingGetTunnelPort      TunnelProgres = "StartingGetTunnelPort"

	// Completed get tunnel port.
	TunnelProgresCompletedGetTunnelPort     TunnelProgres = "CompletedGetTunnelPort"
)
