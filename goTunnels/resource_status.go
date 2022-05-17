// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ResourceStatus.cs

package goTunnels

// Current value and limit for a limited resource related to a tunnel or tunnel port.
type ResourceStatus struct {
	// Gets or sets the current value.
	Current uint64 `json:"current"`

	// Gets or sets the limit enforced by the service, or null if there is no limit.
	//
	// Any requests that would cause the limit to be exceeded may be denied by the service.
	// For HTTP requests, the response is generally a 403 Forbidden status, with details
	// about the limit in the response body.
	Limit uint64 `json:"limit,omitempty"`

	RateStatus
}

// Current value and limit information for a rate-limited operation related to a tunnel or
// port.
type RateStatus struct {
	// Gets or sets the length of each period, in seconds, over which the rate is measured.
	//
	// For rates that are limited by month (or billing period), this value may represent an
	// estimate, since the actual duration may vary by the calendar.
	PeriodSeconds uint32 `json:"periodSeconds,omitempty"`

	// Gets or sets the number of seconds until the current measurement period ends and the
	// current rate value resets.
	ResetSeconds uint32 `json:"resetSeconds,omitempty"`
}
