// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelAuthenticationSchemes.cs

package tunnels

// Defines string constants for authentication schemes supported by tunnel service APIs.
type TunnelAuthenticationSchemes []TunnelAuthenticationScheme
type TunnelAuthenticationScheme string

const (
	// Authentication scheme for AAD (or Microsoft account) access tokens.
	TunnelAuthenticationSchemeAad        TunnelAuthenticationScheme = "aad"

	// Authentication scheme for GitHub access tokens.
	TunnelAuthenticationSchemeGitHub     TunnelAuthenticationScheme = "github"

	// Authentication scheme for tunnel access tokens.
	TunnelAuthenticationSchemeTunnel     TunnelAuthenticationScheme = "tunnel"

	// Authentication scheme for tunnelPlan access tokens.
	TunnelAuthenticationSchemeTunnelPlan TunnelAuthenticationScheme = "tunnelplan"
)
