// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelSshKeyResponse.cs

package tunnels

// Response for SshKey endpoint.
type TunnelSshKeyResponse struct {
	// Gets or sets the ssh key for a tunnel.
	SshKey string `json:"sshKey,omitempty"`
}
