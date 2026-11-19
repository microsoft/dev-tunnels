// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"net/http"
	"net/url"
	"strings"
	"testing"
)

// recordedRequest captures what the client sent so tests can assert on auth and headers.
type recordedRequest struct {
	path          string
	authorization string
	clusterSource string
}

// newRecordingManager returns a Manager whose transport is driven by the given handler and
// records every request it makes. A round-tripper is used rather than a real test server
// because the create path rewrites the request host to include the cluster ID, which no
// local server can serve.
func newRecordingManager(
	t *testing.T,
	token string,
	handler func(r *http.Request) (*http.Response, error),
) (*Manager, *[]recordedRequest) {
	t.Helper()

	serviceURL, err := url.Parse("https://example.test/")
	if err != nil {
		t.Fatalf("parsing url: %v", err)
	}

	var requests []recordedRequest
	client := &http.Client{Transport: roundTripperFunc(func(r *http.Request) (*http.Response, error) {
		requests = append(requests, recordedRequest{
			path:          r.URL.Path,
			authorization: r.Header.Get("Authorization"),
			clusterSource: r.Header.Get(clusterSourceHeaderName),
		})
		return handler(r)
	})}

	manager, err := NewManager(
		userAgentManagerTest, func() string { return token }, serviceURL, client, "2023-09-27-preview")
	if err != nil {
		t.Fatalf("creating manager: %v", err)
	}

	return manager, &requests
}

func isRecommendationsRequest(r *http.Request) bool {
	return strings.Contains(r.URL.Path, "recommendations")
}

// createResponse returns a minimal created-tunnel response for the requested tunnel.
func createResponse(r *http.Request) (*http.Response, error) {
	return responseWithStatus(http.StatusOK,
		`{"tunnelId":"`+tunnelIDFromPath(r.URL.Path)+`"}`), nil
}

// recommendationResponse returns a recommendations response naming the given cluster, or
// an empty recommendation when clusterID is empty.
func recommendationResponse(clusterID string) (*http.Response, error) {
	if clusterID == "" {
		return responseWithStatus(http.StatusOK, `{"recommendations":[]}`), nil
	}
	return responseWithStatus(http.StatusOK,
		`{"recommendedClusterId":"`+clusterID+`","recommendations":[]}`), nil
}

// lastClusterSource returns the cluster-source header from the final recorded request,
// which is the create itself.
func lastClusterSource(requests []recordedRequest) string {
	if len(requests) == 0 {
		return ""
	}
	return requests[len(requests)-1].clusterSource
}

// The recommendations call must send the caller's token so the service can identify them
// and apply their service tier. Before this change it was always anonymous, so every
// caller looked untiered.
func TestGetClusterRecommendationsSendsAuthorizationHeader(t *testing.T) {
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		return recommendationResponse("euw")
	})

	recommendations, err := manager.GetClusterRecommendations(context.Background(), "", "")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if recommendations.RecommendedClusterID != "euw" {
		t.Errorf("got cluster %q, want %q", recommendations.RecommendedClusterID, "euw")
	}
	if len(*requests) != 1 {
		t.Fatalf("got %d requests, want 1", len(*requests))
	}
	if (*requests)[0].authorization != "Bearer test-token" {
		t.Errorf("got Authorization %q, want %q", (*requests)[0].authorization, "Bearer test-token")
	}
}

// With no token configured the call must still work anonymously, and must not send an
// empty Authorization header, which the service would reject.
func TestGetClusterRecommendationsWithoutTokenSendsNoAuthorizationHeader(t *testing.T) {
	manager, requests := newRecordingManager(t, "", func(r *http.Request) (*http.Response, error) {
		return recommendationResponse("euw")
	})

	if _, err := manager.GetClusterRecommendations(context.Background(), "", ""); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(*requests) != 1 {
		t.Fatalf("got %d requests, want 1", len(*requests))
	}
	if (*requests)[0].authorization != "" {
		t.Errorf("got Authorization %q, want none", (*requests)[0].authorization)
	}
}

// The service rejects a bad token before the controller runs, so it never falls back to
// treating the caller as anonymous. Without the retry, one expired token would silently
// disable recommendation-based routing for that caller.
func TestGetClusterRecommendationsRetriesAnonymouslyAfterUnauthorized(t *testing.T) {
	manager, requests := newRecordingManager(t, "Bearer expired-token", func(r *http.Request) (*http.Response, error) {
		if r.Header.Get("Authorization") != "" {
			return responseWithStatus(http.StatusUnauthorized, `{"title":"Unauthorized"}`), nil
		}
		return recommendationResponse("euw")
	})

	recommendations, err := manager.GetClusterRecommendations(context.Background(), "", "")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if recommendations.RecommendedClusterID != "euw" {
		t.Errorf("got cluster %q, want %q", recommendations.RecommendedClusterID, "euw")
	}
	if len(*requests) != 2 {
		t.Fatalf("got %d requests, want 2 (authenticated then anonymous)", len(*requests))
	}
	if (*requests)[1].authorization != "" {
		t.Errorf("retry sent Authorization %q, want none", (*requests)[1].authorization)
	}
}

// A non-auth failure must not trigger the anonymous retry; retrying would not help and
// would double the latency of every failure.
func TestGetClusterRecommendationsDoesNotRetryOnServerError(t *testing.T) {
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		return responseWithStatus(http.StatusInternalServerError, `{"title":"Server error"}`), nil
	})

	if _, err := manager.GetClusterRecommendations(context.Background(), "", ""); err == nil {
		t.Fatal("expected an error")
	}
	if len(*requests) != 1 {
		t.Errorf("got %d requests, want 1 (no retry)", len(*requests))
	}
}

// When the caller specifies a cluster there is nothing to recommend, so no call is made
// and the create reports that the cluster was chosen explicitly.
func TestCreateTunnelWithExplicitClusterReportsExplicitSource(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			t.Error("recommendations should not be requested when a cluster is specified")
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{ClusterID: "euw"}, nil); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(selections) != 0 {
		t.Errorf("got %d selection callbacks, want 0", len(selections))
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceExplicit) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceExplicit)
	}
}

// The happy path: the recommendation is used and reported as such.
func TestCreateTunnelReportsRecommendedCluster(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			return recommendationResponse("euw")
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(selections) != 1 {
		t.Fatalf("got %d selection callbacks, want 1", len(selections))
	}
	if selections[0].Source != ClusterSourceRecommended {
		t.Errorf("got source %q, want %q", selections[0].Source, ClusterSourceRecommended)
	}
	if selections[0].ClusterID != "euw" {
		t.Errorf("got cluster %q, want %q", selections[0].ClusterID, "euw")
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceRecommended) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceRecommended)
	}
}

// Routing is correct here but the caller was not identified, so they cannot be assigned a
// service tier. That is a token problem the caller would otherwise never learn about.
func TestCreateTunnelReportsRecommendationAfterAuthRejected(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer expired-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			if r.Header.Get("Authorization") != "" {
				return responseWithStatus(http.StatusUnauthorized, `{"title":"Unauthorized"}`), nil
			}
			return recommendationResponse("euw")
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(selections) != 1 {
		t.Fatalf("got %d selection callbacks, want 1", len(selections))
	}
	if selections[0].Source != ClusterSourceRecommendedAfterAuthRejected {
		t.Errorf("got source %q, want %q", selections[0].Source, ClusterSourceRecommendedAfterAuthRejected)
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceRecommendedAfterAuthRejected) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceRecommendedAfterAuthRejected)
	}
}

// The create still succeeds via global routing, so the fallback is invisible without the
// callback and the header.
func TestCreateTunnelReportsFallbackOnRecommendationError(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			return responseWithStatus(http.StatusInternalServerError, `{"title":"Server error"}`), nil
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	tunnel, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil)
	if err != nil {
		t.Fatalf("create should still succeed via global routing: %v", err)
	}
	if tunnel == nil {
		t.Fatal("expected a tunnel")
	}
	if len(selections) != 1 {
		t.Fatalf("got %d selection callbacks, want 1", len(selections))
	}
	if selections[0].Source != ClusterSourceFallbackError {
		t.Errorf("got source %q, want %q", selections[0].Source, ClusterSourceFallbackError)
	}
	if selections[0].Err == nil {
		t.Error("expected the underlying error to be reported")
	}
	if !selections[0].Source.IsFallback() {
		t.Error("expected the source to be classified as a fallback")
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceFallbackError) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceFallbackError)
	}
}

// An auth failure that survives the anonymous retry is distinguished from a generic
// failure, because it points at the service rejecting the request rather than being down.
func TestCreateTunnelReportsFallbackWhenAuthFailsEvenAnonymously(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			return responseWithStatus(http.StatusUnauthorized, `{"title":"Unauthorized"}`), nil
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil); err != nil {
		t.Fatalf("create should still succeed via global routing: %v", err)
	}
	if len(selections) != 1 {
		t.Fatalf("got %d selection callbacks, want 1", len(selections))
	}
	if selections[0].Source != ClusterSourceFallbackAuthFailed {
		t.Errorf("got source %q, want %q", selections[0].Source, ClusterSourceFallbackAuthFailed)
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceFallbackAuthFailed) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceFallbackAuthFailed)
	}
}

// A successful call that recommends nothing is a distinct condition from a failure, and
// means the service had no cluster to offer.
func TestCreateTunnelReportsFallbackOnEmptyRecommendation(t *testing.T) {
	var selections []ClusterSelection
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			return recommendationResponse("")
		}
		return createResponse(r)
	})
	manager.OnClusterSelected = func(s ClusterSelection) { selections = append(selections, s) }

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(selections) != 1 {
		t.Fatalf("got %d selection callbacks, want 1", len(selections))
	}
	if selections[0].Source != ClusterSourceFallbackEmpty {
		t.Errorf("got source %q, want %q", selections[0].Source, ClusterSourceFallbackEmpty)
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceFallbackEmpty) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceFallbackEmpty)
	}
}

// The callback is optional, so a nil callback must not panic and the header must still be
// sent.
func TestCreateTunnelWithoutCallbackStillSendsHeader(t *testing.T) {
	manager, requests := newRecordingManager(t, "Bearer test-token", func(r *http.Request) (*http.Response, error) {
		if isRecommendationsRequest(r) {
			return recommendationResponse("euw")
		}
		return createResponse(r)
	})

	if _, err := manager.CreateTunnel(context.Background(), &Tunnel{}, nil); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if got := lastClusterSource(*requests); got != string(ClusterSourceRecommended) {
		t.Errorf("got cluster source header %q, want %q", got, ClusterSourceRecommended)
	}
}
