// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ErrorDetail.cs

package tunnels

// The top-level error object whose code matches the x-ms-error-code response header
type ErrorDetail struct {
	// One of a server-defined set of error codes defined in `ErrorCodes`.
	Code       string `json:"code"`

	// A human-readable representation of the error.
	Message    string `json:"message"`

	// The target of the error.
	Target     string `json:"target,omitempty"`

	// An array of details about specific errors that led to this reported error.
	Details    []ErrorDetail `json:"details,omitempty"`

	// An object containing more specific information than the current object about the
	// error.
	InnerError *InnerErrorDetail `json:"innererror,omitempty"`
}
