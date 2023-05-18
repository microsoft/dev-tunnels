// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ResourceStatus.cs

package tunnels

// Current value and limit for a limited resource related to a tunnel or tunnel port.
type ResourceStatus struct {
	// Gets or sets the current value.
	Current     uint64 `json:"current"`

	// Gets or sets the limit enforced by the service, or null if there is no limit.
	//
	// Any requests that would cause the limit to be exceeded may be denied by the service.
	// For HTTP requests, the response is generally a 403 Forbidden status, with details
	// about the limit in the response body.
	Limit       uint64 `json:"limit,omitempty"`

	// Gets or sets an optional source of the `ResourceStatus.Limit`, or null if there is no
	// limit.
	LimitSource string `json:"limitSource,omitempty"`

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

	// Gets or sets the unix time in seconds when this status will be reset.
	ResetTime     int64 `json:"resetTime,omitempty"`

	NamedRateStatus
}

// A named `RateStatus`.
type NamedRateStatus struct {
	// The name of the rate status.
	Name string `json:"name"`
}
