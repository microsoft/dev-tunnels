// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ErrorCodes.cs

package tunnels

// Error codes for ErrorDetail.Code and `x-ms-error-code` header.
type ErrorCodes []ErrorCode
type ErrorCode string

const (
	// Operation timed out.
	ErrorCodeTimeout            ErrorCode = "Timeout"

	// Operation cannot be performed because the service is not available.
	ErrorCodeServiceUnavailable ErrorCode = "ServiceUnavailable"

	// Internal error.
	ErrorCodeInternalError      ErrorCode = "InternalError"
)
