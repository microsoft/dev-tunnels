// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ProblemDetails.cs

package tunnels

// Structure of error details returned by the tunnel service, including validation errors.
//
// This object may be returned with a response status code of 400 (or other 4xx code). It
// is compatible with RFC 7807 Problem Details (https://tools.ietf.org/html/rfc7807) and
// https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails but
// doesn't require adding a dependency on that package.
type ProblemDetails struct {
	// Gets or sets the error title.
	Title  string `json:"title,omitempty"`

	// Gets or sets the error detail.
	Detail string `json:"detail,omitempty"`

	// Gets or sets additional details about individual request properties.
	Errors map[string][]string `json:"errors,omitempty"`
}
