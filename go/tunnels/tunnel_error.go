// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import "fmt"

// TunnelError represents an error response from the tunnel service
// with an HTTP status code.
type TunnelError struct {
	StatusCode int
	Message    string
}

func (e *TunnelError) Error() string {
	return fmt.Sprintf("tunnel service error (status %d): %s", e.StatusCode, e.Message)
}
