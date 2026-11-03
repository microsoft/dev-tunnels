// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

// ClusterSource describes how the cluster for a tunnel create request was chosen.
//
// When a create request does not specify a cluster, the client asks the recommendations
// API which cluster to use. That call can fail, and when it does the client falls back to
// global (Traffic Manager) routing, which still works but picks the nearest cluster by
// latency rather than the recommended one. The fallback is therefore invisible to the
// caller, so this value records which path was actually taken.
type ClusterSource string

const (
	// ClusterSourceExplicit means the caller specified the cluster, so no recommendation
	// was requested.
	ClusterSourceExplicit ClusterSource = "explicit"

	// ClusterSourceRecommended means the recommendations API was called and its cluster
	// was used.
	ClusterSourceRecommended ClusterSource = "recommended"

	// ClusterSourceRecommendedAfterAuthRejected means the recommendations API rejected the
	// caller's token and the retry without a token succeeded. Routing is correct but the
	// caller was not identified, so it is treated as anonymous and cannot be assigned a
	// service tier. This indicates a token problem that would otherwise be invisible.
	ClusterSourceRecommendedAfterAuthRejected ClusterSource = "recommended-after-auth-rejected"

	// ClusterSourceFallbackAuthFailed means the recommendations API returned unauthorized
	// even without a token, so global routing was used instead.
	ClusterSourceFallbackAuthFailed ClusterSource = "fallback-auth-failed"

	// ClusterSourceFallbackEmpty means the recommendations API returned no cluster, so
	// global routing was used instead.
	ClusterSourceFallbackEmpty ClusterSource = "fallback-empty"

	// ClusterSourceFallbackError means the recommendations API call failed, so global
	// routing was used instead.
	ClusterSourceFallbackError ClusterSource = "fallback-error"
)

// IsFallback reports whether the tunnel was placed by global routing rather than by
// recommendation.
func (s ClusterSource) IsFallback() bool {
	return s == ClusterSourceFallbackAuthFailed ||
		s == ClusterSourceFallbackEmpty ||
		s == ClusterSourceFallbackError
}

// ClusterSelection reports how the cluster for a tunnel create request was chosen.
type ClusterSelection struct {
	// Source is how the cluster was chosen.
	Source ClusterSource

	// ClusterID is the cluster that was selected, if one was.
	ClusterID string

	// Err is the failure that caused a fallback, if there was one.
	Err error
}
