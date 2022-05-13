// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelServiceProperties.cs

package tunnels

// Provides environment-dependent properties about the service.
type TunnelServiceProperties struct {
	// Gets the base URI of the service.
	ServiceURI           string `json:"serviceUri"`

	// Gets the public AAD AppId for the service.
	//
	// Clients specify this AppId as the audience property when authenticating to the
	// service.
	ServiceAppID         string `json:"serviceAppId"`

	// Gets the internal AAD AppId for the service.
	//
	// Other internal services specify this AppId as the audience property when
	// authenticating to the tunnel service. Production services must be in the AME tenant to
	// use this appid.
	ServiceInternalAppID string `json:"serviceInternalAppId"`

	// Gets the client ID for the service's GitHub app.
	//
	// Clients apps that authenticate tunnel users with GitHub specify this as the client ID
	// when requesting a user token.
	GitHubAppClientID    string `json:"gitHubAppClientId"`
}

// Global DNS name of the production tunnel service.
var prodDnsName = "global.rel.tunnels.api.visualstudio.com"

// Global DNS name of the pre-production tunnel service.
var ppeDnsName = "global.rel.tunnels.ppe.api.visualstudio.com"

// Global DNS name of the development tunnel service.
var devDnsName = "global.ci.tunnels.dev.api.visualstudio.com"

// First-party app ID: `Visual Studio Tunnel Service`
//
// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
// in the PROD service environment.
var prodFirstPartyAppID = "46da2f7e-b5ef-422a-88d4-2a7f9de6a0b2"

// First-party app ID: `Visual Studio Tunnel Service - Test`
//
// Used for authenticating AAD/MSA users, and service principals outside the AME tenant,
// in the PPE and DEV service environments.
var nonProdFirstPartyAppID = "54c45752-bacd-424a-b928-652f3eca2b18"

// Third-party app ID: `tunnels-prod-app-sp`
//
// Used for authenticating internal AAD service principals in the AME tenant, in the PROD
// service environment.
var prodThirdPartyAppID = "ce65d243-a913-4cae-a7dd-cb52e9f77647"

// Third-party app ID: `tunnels-ppe-app-sp`
//
// Used for authenticating internal AAD service principals in the AME tenant, in the PPE
// service environment.
var ppeThirdPartyAppID = "544167a6-f431-4518-aac6-2fd50071928e"

// Third-party app ID: `tunnels-dev-app-sp`
//
// Used for authenticating internal AAD service principals in the corp tenant (not AME!),
// in the DEV service environment.
var devThirdPartyAppID = "a118c979-0249-44bb-8f95-eb0457127aeb"

// GitHub App Client ID for 'Visual Studio Tunnel Service'
//
// Used by client apps that authenticate tunnel users with GitHub, in the PROD service
// environment.
var prodGitHubAppClientID = "Iv1.e7b89e013f801f03"

// GitHub App Client ID for 'Visual Studio Tunnel Service - Test'
//
// Used by client apps that authenticate tunnel users with GitHub, in the PPE and DEV
// service environments.
var nonProdGitHubAppClientID = "Iv1.b231c327f1eaa229"
