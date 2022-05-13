// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ServiceVersionDetails.cs

package tunnels

// Data contract for service version details.
type ServiceVersionDetails struct {
	// Gets or sets the version of the service. E.g. "1.0.6615.53976". The version
	// corresponds to the build number.
	Version       string `json:"version"`

	// Gets or sets the commit ID of the service.
	CommitID      string `json:"commitId"`

	// Gets or sets the commit date of the service.
	CommitDate    string `json:"commitDate"`

	// Gets or sets the cluster ID of the service that handled the request.
	ClusterID     string `json:"clusterId"`

	// Gets or sets the Azure location of the service that handled the request.
	AzureLocation string `json:"azureLocation"`
}
