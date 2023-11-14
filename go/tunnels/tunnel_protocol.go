// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelProtocol.cs

package tunnels

// Defines possible values for the protocol of a `TunnelPort`.
type TunnelProtocol string

const (
	// The protocol is automatically detected. (TODO: Define detection semantics.)
	TunnelProtocolAuto  TunnelProtocol = "auto"

	// Unknown TCP protocol.
	TunnelProtocolTcp   TunnelProtocol = "tcp"

	// Unknown UDP protocol.
	TunnelProtocolUdp   TunnelProtocol = "udp"

	// SSH protocol.
	TunnelProtocolSsh   TunnelProtocol = "ssh"

	// Remote desktop protocol.
	TunnelProtocolRdp   TunnelProtocol = "rdp"

	// HTTP protocol.
	TunnelProtocolHttp  TunnelProtocol = "http"

	// HTTPS protocol.
	TunnelProtocolHttps TunnelProtocol = "https"
)
