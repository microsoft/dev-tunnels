// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"math/rand"
	"net/http"
	"net/url"
	"os"
	"strings"
	"testing"
	"time"
)

var (
	serviceUrl           = ServiceProperties.ServiceURI
	ctx                  = context.Background()
	userAgentManagerTest = []UserAgent{{Name: "Tunnels-Go-SDK-Tests/Manager", Version: PackageVersion}}
)

func getUserToken() string {
	// Obtain a token using `devtunnel user show --verbose` and paste it here to enable
	// integration tests. Format: "github <gh-token>" or "Bearer <aad-token>"
	return ""
}

func failIfNoToken(t *testing.T) {
	if getUserToken() == "" {
		t.Fatal("No user token configured. Update getUserToken() to enable integration tests.")
	}
}

func logVerbose(t *testing.T, logger *log.Logger, format string, args ...interface{}) {
	if testing.Verbose() {
		logger.Printf(format, args...)
	}
}

// These tests do not automatically run in the PR check github action
// because they require authentication. If you want to run these tests
// you must first generate a tunnels access token and paste it in the
// getUserToken return value.
func TestTunnelCreateDelete(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

type roundTripperFunc func(*http.Request) (*http.Response, error)

func (f roundTripperFunc) RoundTrip(r *http.Request) (*http.Response, error) {
	return f(r)
}

func responseWithStatus(status int, body string) *http.Response {
	return &http.Response{
		StatusCode: status,
		Body:       io.NopCloser(strings.NewReader(body)),
		Header:     make(http.Header),
	}
}

func tunnelIDFromPath(path string) string {
	trimmed := strings.Trim(path, "/")
	parts := strings.Split(trimmed, "/")
	if len(parts) == 0 {
		return ""
	}
	return parts[len(parts)-1]
}

func TestCreateTunnelRetriesOnConflictForGeneratedID(t *testing.T) {
	url, err := url.Parse("https://example.test/")
	if err != nil {
		t.Fatalf("error parsing url: %v", err)
	}

	originalAdjectives := adjectives
	originalNouns := nouns
	adjectives = []string{"alpha"}
	nouns = []string{"one"}
	defer func() {
		adjectives = originalAdjectives
		nouns = originalNouns
	}()

	var pathIDs []string
	var bodyIDs []string
	callCount := 0
	client := &http.Client{Transport: roundTripperFunc(func(r *http.Request) (*http.Response, error) {
		callCount++
		pathIDs = append(pathIDs, tunnelIDFromPath(r.URL.Path))
		bodyBytes, readErr := io.ReadAll(r.Body)
		if readErr == nil {
			var payload map[string]string
			if jsonErr := json.Unmarshal(bodyBytes, &payload); jsonErr == nil {
				bodyIDs = append(bodyIDs, payload["tunnelId"])
			}
		}
		if callCount == 1 {
			adjectives = []string{"beta"}
			nouns = []string{"two"}
			return responseWithStatus(http.StatusConflict, ""), nil
		}
		id := tunnelIDFromPath(r.URL.Path)
		body := fmt.Sprintf("{\"tunnelId\":\"%s\"}", id)
		return responseWithStatus(http.StatusOK, body), nil
	})}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, client, "2023-09-27-preview")
	if err != nil {
		t.Fatalf("error creating manager: %v", err)
	}

	tunnel := &Tunnel{}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, nil)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if callCount != 2 {
		t.Fatalf("expected 2 requests, got %d", callCount)
	}
	if len(pathIDs) != 2 {
		t.Fatalf("expected 2 path IDs, got %d", len(pathIDs))
	}
	if len(bodyIDs) != 2 {
		t.Fatalf("expected 2 body IDs, got %d", len(bodyIDs))
	}
	if pathIDs[0] == pathIDs[1] {
		t.Fatalf("expected retry to use a different tunnel ID")
	}
	if pathIDs[0] != bodyIDs[0] || pathIDs[1] != bodyIDs[1] {
		t.Fatalf("expected request path ID to match request body tunnelId")
	}
	if createdTunnel.TunnelID == "" {
		t.Fatalf("expected tunnel ID to be set")
	}
}

func TestCreateTunnelDoesNotRetryOnNonConflict(t *testing.T) {
	url, err := url.Parse("https://example.test/")
	if err != nil {
		t.Fatalf("error parsing url: %v", err)
	}

	callCount := 0
	client := &http.Client{Transport: roundTripperFunc(func(r *http.Request) (*http.Response, error) {
		callCount++
		return responseWithStatus(http.StatusInternalServerError, ""), nil
	})}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, client, "2023-09-27-preview")
	if err != nil {
		t.Fatalf("error creating manager: %v", err)
	}

	_, err = managementClient.CreateTunnel(context.Background(), &Tunnel{}, nil)
	if err == nil {
		t.Fatalf("expected error")
	}
	if callCount != 1 {
		t.Fatalf("expected 1 request, got %d", callCount)
	}
}

func TestCreateTunnelDoesNotRetryWhenIDProvided(t *testing.T) {
	url, err := url.Parse("https://example.test/")
	if err != nil {
		t.Fatalf("error parsing url: %v", err)
	}

	callCount := 0
	client := &http.Client{Transport: roundTripperFunc(func(r *http.Request) (*http.Response, error) {
		callCount++
		return responseWithStatus(http.StatusConflict, ""), nil
	})}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, client, "2023-09-27-preview")
	if err != nil {
		t.Fatalf("error creating manager: %v", err)
	}

	_, err = managementClient.CreateTunnel(context.Background(), &Tunnel{TunnelID: "provided-id"}, nil)
	if err == nil {
		t.Fatalf("expected error")
	}
	if callCount != 1 {
		t.Fatalf("expected 1 request, got %d", callCount)
	}
}

func TestListTunnels(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	var token string
	if createdTunnel.AccessTokens != nil {
		token = createdTunnel.AccessTokens["manage"]
	} else {
		logVerbose(t, logger, "Did not get token for created tunnel")
	}
	options = &TunnelRequestOptions{
		AccessToken: token,
	}
	tunnels, err := managementClient.ListTunnels(ctx, "", "", options)
	if err != nil {
		t.Errorf(err.Error())
	}
	if len(tunnels) == 0 {
		t.Errorf("tunnel was not successfully listed")
	}
	for _, tunnel := range tunnels {
		logVerbose(t, logger, "found tunnel with id %s", tunnel.TunnelID)
		logVerbose(t, logger, "%v", tunnel.Table())
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestGetAccessToken(t *testing.T) {
	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	emptyTokenProvider := func() string { return "" }
	managementClient, err := NewManager(userAgentManagerTest, emptyTokenProvider, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeConnect: "connect_token",
			TunnelAccessScopeManage:  "manage_token",
		},
	}

	// Test that the connect scope returns the connect token
	token := managementClient.getAccessToken(tunnel, &TunnelRequestOptions{}, []TunnelAccessScope{TunnelAccessScopeConnect})
	if token != "Tunnel connect_token" {
		t.Errorf("connect token was not successfully retrieved, got %s", token)
	}

	// Test that the manage scope returns the manage token
	token = managementClient.getAccessToken(tunnel, &TunnelRequestOptions{}, []TunnelAccessScope{TunnelAccessScopeManage})
	if token != "Tunnel manage_token" {
		t.Errorf("manage token was not successfully retrieved, got %s", token)
	}

	// Test that when providing multiple scopes (manage:ports, connect, manage), either of the tokens is returned (since maps don't guarantee iteration order)
	token = managementClient.getAccessToken(tunnel, &TunnelRequestOptions{}, []TunnelAccessScope{TunnelAccessScopeManagePorts, TunnelAccessScopeConnect, TunnelAccessScopeManage})
	if token != "Tunnel connect_token" && token != "Tunnel manage_token" {
		t.Errorf("token was not successfully retrieved, got %s", token)
	}

	// Update the tunnel to use a space delimited string for the access token type
	tunnel = &Tunnel{
		AccessTokens: map[TunnelAccessScope]string{
			"connect manage": "connect_and_manage_token",
		},
	}

	// Test that the connect scope returns the token
	token = managementClient.getAccessToken(tunnel, &TunnelRequestOptions{}, []TunnelAccessScope{TunnelAccessScopeConnect})
	if token != "Tunnel connect_and_manage_token" {
		t.Errorf("token was not successfully retrieved, got %s", token)
	}

	// Test that the manage scope returns the token
	token = managementClient.getAccessToken(tunnel, &TunnelRequestOptions{}, []TunnelAccessScope{TunnelAccessScopeManage})
	if token != "Tunnel connect_and_manage_token" {
		t.Errorf("token was not successfully retrieved, got %s", token)
	}
}

func TestTunnelCreateUpdateDelete(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	s1 := rand.NewSource(time.Now().UnixNano())
	r1 := rand.New(s1)
	generatedName := fmt.Sprintf("test%d", r1.Intn(10000))
	createdTunnel.Name = generatedName
	updatedTunnel, err := managementClient.UpdateTunnel(ctx, createdTunnel, []string{"Name"}, options)
	if err != nil {
		t.Errorf("tunnel was not successfully updated: %s", err.Error())
	} else if updatedTunnel.Name != generatedName {
		t.Errorf("tunnel was not successfully updated")
	} else {
		logVerbose(t, logger, "Tunnel updated")
		logVerbose(t, logger, "%v", updatedTunnel.Table())
	}
	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelCreateUpdateTwiceDelete(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	s1 := rand.NewSource(time.Now().UnixNano())
	r1 := rand.New(s1)
	generatedName := fmt.Sprintf("test%d", r1.Intn(10000))
	createdTunnel.Name = generatedName
	updatedTunnel, err := managementClient.UpdateTunnel(ctx, createdTunnel, []string{"Name"}, options)
	if err != nil {
		t.Errorf("tunnel was not successfully updated: %s", err.Error())
	} else if updatedTunnel.Name != generatedName {
		t.Errorf("tunnel was not successfully updated")
	} else {
		logVerbose(t, logger, "Tunnel updated")
		logVerbose(t, logger, "%v", updatedTunnel.Table())
	}

	// In the second update we want to update the description without updating the name
	createdTunnel.Name = ""
	createdTunnel.Description = "test description"
	updatedTunnel, err = managementClient.UpdateTunnel(ctx, createdTunnel, []string{"Description"}, options)
	if err != nil {
		t.Errorf("tunnel was not successfully updated: %s", err.Error())
	} else if updatedTunnel.Name != generatedName || updatedTunnel.Description != "test description" {
		t.Errorf("tunnel was not successfully updated")
	} else {
		logVerbose(t, logger, "Tunnel updated")
		logVerbose(t, logger, "%v", updatedTunnel.Table())
	}
	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelCreateGetDelete(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", getTunnel.TunnelID)
	}
}

func TestTunnelAddPort(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)

	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "Created port: %d", port.PortNumber)
	logVerbose(t, logger, "%v", port.Table())

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}

	if len(getTunnel.Ports) != 1 {
		t.Errorf("port was not successfully added to tunnel")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelDeletePort(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)

	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "Created port: %d", port.PortNumber)
	logVerbose(t, logger, "%v", port.Table())

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}

	err = managementClient.DeleteTunnelPort(ctx, createdTunnel, 3000, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "Deleted port: %d", port.PortNumber)

	getTunnel, err = managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}

	if len(getTunnel.Ports) != 0 {
		t.Errorf("port was not successfully deleted")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelUpdatePort(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)

	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true, TokenScopes: []TunnelAccessScope{"manage"}}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "Created port: %d", port.PortNumber)
	logVerbose(t, logger, "%v", port.Table())

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}
	accessEntry := TunnelAccessControlEntry{
		Type:     TunnelAccessControlEntryTypeAnonymous,
		Subjects: []string{},
		Scopes:   []string{string(TunnelAccessScopeManage)},
	}
	portToAdd.AccessControl = &TunnelAccessControl{
		Entries: make([]TunnelAccessControlEntry, 0),
	}
	portToAdd.AccessControl.Entries = append(port.AccessControl.Entries, accessEntry)

	port, err = managementClient.UpdateTunnelPort(ctx, createdTunnel, portToAdd, nil, options)
	if err != nil {
		t.Errorf("port was not successfully updated: %s", err)
	} else if len(port.AccessControl.Entries) != 1 {
		t.Errorf("port was not successfully updated")
	}

	getTunnel, err = managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}
	if len(getTunnel.Ports[0].AccessControl.Entries) != 1 {
		t.Errorf("tunnel port was not successfully updated, access control was not changed")
	}

	port, err = managementClient.GetTunnelPort(ctx, createdTunnel, 3000, options)
	if err != nil {
		t.Errorf("port get error %s", err.Error())
		return
	}
	if len(port.AccessControl.Entries) != 1 {
		t.Errorf("tunnel port was not successfully updated, access control was not changed")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelListPorts(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)

	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}
	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}

	logVerbose(t, logger, "Created port: %d", port.PortNumber)
	logVerbose(t, logger, "%v", port.Table())

	portToAdd = NewTunnelPort(3001, "", "", "auto")
	port, err = managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "Created port: %d", port.PortNumber)
	logVerbose(t, logger, "%v", port.Table())

	ports, err := managementClient.ListTunnelPorts(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if len(ports) != 2 {
		t.Errorf("ports not successfully listed")
	}

	if ports[0].PortNumber != 3000 {
		t.Errorf("port 3000 not successfully listed")
	}

	if ports[1].PortNumber != 3001 {
		t.Errorf("port 3001 not successfully listed")
	}

	for _, port := range ports {
		logVerbose(t, logger, "Port: %d", port.PortNumber)
		logVerbose(t, logger, "%v", port.Table())
	}

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
		logVerbose(t, logger, "%v", getTunnel.Table())
	}

	if len(getTunnel.Ports) != 2 {
		t.Errorf("port was not successfully added to tunnel")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", createdTunnel.TunnelID)
	}
}

func TestTunnelEndpoints(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping test in short mode.")
	}
	failIfNoToken(t)

	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(serviceUrl)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getUserToken, url, nil, "2023-09-27-preview")
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{
		TokenScopes: managePortsAccessTokenScopes,
	}
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logVerbose(t, logger, "Created tunnel with id %s", createdTunnel.TunnelID)
		logVerbose(t, logger, "%v", createdTunnel.Table())
	}

	// Create and add endpoint
	endpoint := &TunnelEndpoint{
		HostID:         "test",
		ID:             "test",
		ConnectionMode: TunnelConnectionModeTunnelRelay,
	}

	updatedEndpoint, err := managementClient.UpdateTunnelEndpoint(ctx, createdTunnel, endpoint, nil, options)

	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logVerbose(t, logger, "updated endpoint %s", updatedEndpoint.HostID)

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
	}
	if len(getTunnel.Endpoints) != 1 {
		t.Errorf("endpoint was not successfully updated")
	}

	err = managementClient.DeleteTunnelEndpoints(ctx, createdTunnel, "test", options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}

	getTunnel, err = managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logVerbose(t, logger, "Got tunnel with id %s", getTunnel.TunnelID)
	}
	if len(getTunnel.Endpoints) != 0 {
		t.Errorf("endpoint was not successfully deleted")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logVerbose(t, logger, "Deleted tunnel with id %s", getTunnel.TunnelID)
	}
}

func TestResourceStatusUnmarshal(t *testing.T) {
	var test1 = []byte("{ \"current\": 3, \"limit\": 10 }")
	var result1 ResourceStatus
	var err = json.Unmarshal(test1, &result1)
	if err != nil {
		t.Error(err)
	}

	if result1.Limit == 0 {
		t.Errorf("Limit was not deserialized")
	}

	var result2 ResourceStatus
	var test2 = []byte("3")
	err = json.Unmarshal(test2, &result2)
	if err != nil {
		t.Error(err)
	}

	if result1.Current != result2.Current {
		t.Errorf("%d != %d", result1.Current, result2.Current)
	}
}

func TestValidTokenScopes(t *testing.T) {
	var validScopes = TunnelAccessScopes{"host", "connect"}
	var invalidScopes = TunnelAccessScopes{"invalid", "connect"}
	var multiScopes = TunnelAccessScopes{"host connect", "manage"}

	if err := validScopes.valid(nil, false); err != nil {
		t.Error(err)
	}
	if err := invalidScopes.valid(nil, false); err == nil {
		t.Errorf("Invalid scopes should not be valid")
	}
	if err := multiScopes.valid(nil, true); err != nil {
		t.Error(err)
	}
	if err := multiScopes.valid(nil, false); err == nil {
		t.Errorf("Multiple scopes should not be valid without allowMultiple flag")
	}
}

func TestCustomDomainDoesNotModifyHostname(t *testing.T) {
	manager, err := NewManagerForCustomDomain(
		"app.github.dev",
		userAgentManagerTest,
		getUserToken,
		nil,
		"2023-09-27-preview",
	)
	if err != nil {
		t.Fatalf("Failed to create manager: %v", err)
	}

	tunnel := &Tunnel{
		TunnelID:  "tnnl0001",
		ClusterID: "usw2",
	}
	uri := manager.buildUri(tunnel.ClusterID, fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.TunnelID), nil, "")
	if uri.Hostname() != "cp.app.github.dev" {
		t.Errorf("Expected hostname cp.app.github.dev, got %s", uri.Hostname())
	}
}

func TestStandardServiceUriReplacesClusterId(t *testing.T) {
	serviceUrl, _ := url.Parse(ServiceProperties.ServiceURI)
	manager, err := NewManager(
		userAgentManagerTest,
		getUserToken,
		serviceUrl,
		nil,
		"2023-09-27-preview",
	)
	if err != nil {
		t.Fatalf("Failed to create manager: %v", err)
	}

	tunnel := &Tunnel{
		TunnelID:  "tnnl0001",
		ClusterID: "usw2",
	}
	uri := manager.buildUri(tunnel.ClusterID, fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.TunnelID), nil, "")
	if !strings.HasPrefix(uri.Hostname(), "usw2.") {
		t.Errorf("Expected hostname to start with usw2., got %s", uri.Hostname())
	}
}
