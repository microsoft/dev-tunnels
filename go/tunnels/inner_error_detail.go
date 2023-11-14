// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/InnerErrorDetail.cs

package tunnels

// An object containing more specific information than the current object about the error.
type InnerErrorDetail struct {
	// A more specific error code than was provided by the containing error. One of a
	// server-defined set of error codes in `ErrorCodes`.
	Code       string `json:"code"`

	// An object containing more specific information than the current object about the
	// error.
	InnerError *InnerErrorDetail `json:"innererror,omitempty"`
}
