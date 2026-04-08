// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// E2E tests for the Host API against the live dev-tunnels relay service.
// These tests are skipped in short mode (go test -short).
//
// To run manually:
//
//	TUNNEL_AUTH_TOKEN=<your-token> go test -v -run TestHostE2ELiveRelay
//	TUNNEL_AUTH_TOKEN=<your-token> go test -v -run TestHostAndClientE2ELiveRelay
//
// The auth token must be a valid Azure AD or GitHub token for the dev-tunnels service.
// You can obtain one via the VS Code dev tunnels extension or the `devtunnel` CLI:
//
//	devtunnel user login
//	devtunnel token
package main

import (
	"context"
	"io"
	"log"
	"net"
	"net/url"
	"os"
	"strings"
	"testing"
	"time"

	tunnels "github.com/microsoft/dev-tunnels/go/tunnels"
)

func getTestAuthToken() string {
	if token := os.Getenv("TUNNEL_AUTH_TOKEN"); token != "" {
		return token
	}
	return ""
}

// formatAuthToken ensures the token has the correct scheme prefix.
// GitHub tokens (ghu_) need "github " prefix, AAD tokens need "Bearer " prefix.
func formatAuthToken(token string) string {
	if strings.HasPrefix(token, "github ") || strings.HasPrefix(token, "Bearer ") || strings.HasPrefix(token, "Tunnel ") {
		return token
	}
	if strings.HasPrefix(token, "ghu_") || strings.HasPrefix(token, "gho_") {
		return "github " + token
	}
	return "Bearer " + token
}

func newTestManagerForE2E(t *testing.T) *tunnels.Manager {
	t.Helper()
	token := getTestAuthToken()
	if token == "" {
		t.Skip("TUNNEL_AUTH_TOKEN not set")
	}

	serviceURL, err := url.Parse(tunnels.ServiceProperties.ServiceURI)
	if err != nil {
		t.Fatalf("failed to parse service URL: %v", err)
	}

	userAgents := []tunnels.UserAgent{{Name: "Tunnels-Go-SDK-E2E-Test", Version: "0.0.1"}}
	formattedToken := formatAuthToken(token)
	mgr, err := tunnels.NewManager(userAgents, func() string { return formattedToken }, serviceURL, nil, "2023-09-27-preview")
	if err != nil {
		t.Fatalf("failed to create manager: %v", err)
	}
	return mgr
}

// TestHostE2ELiveRelay validates host lifecycle against the real relay service.
// Skipped in short mode. Requires TUNNEL_AUTH_TOKEN env var.
func TestHostE2ELiveRelay(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping live relay test in short mode")
	}

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	mgr := newTestManagerForE2E(t)
	logger := log.New(os.Stdout, "e2e-host: ", log.LstdFlags)

	// 1. Create a tunnel.
	tunnel, err := mgr.CreateTunnel(ctx, &tunnels.Tunnel{}, nil)
	if err != nil {
		t.Fatalf("CreateTunnel failed: %v", err)
	}
	t.Logf("Created tunnel: %s (cluster: %s)", tunnel.TunnelID, tunnel.ClusterID)

	// Ensure cleanup.
	defer func() {
		cleanupCtx := context.Background()
		if err := mgr.DeleteTunnel(cleanupCtx, tunnel, nil); err != nil {
			t.Logf("Warning: DeleteTunnel failed: %v", err)
		}
	}()

	// Request a host access token.
	tokenOptions := &tunnels.TunnelRequestOptions{
		TokenScopes: []tunnels.TunnelAccessScope{tunnels.TunnelAccessScopeHost},
	}
	tunnel, err = mgr.GetTunnel(ctx, tunnel, tokenOptions)
	if err != nil {
		t.Fatalf("GetTunnel (with host token) failed: %v", err)
	}

	// 2. Create a Host and connect.
	host, err := tunnels.NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	t.Log("Host connected to relay")

	// 3. Start a local echo server and add the port.
	echoListener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("failed to start echo server: %v", err)
	}
	defer echoListener.Close()

	echoPort := uint16(echoListener.Addr().(*net.TCPAddr).Port)
	go func() {
		for {
			conn, err := echoListener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer conn.Close()
				io.Copy(conn, conn)
			}()
		}
	}()

	port := &tunnels.TunnelPort{PortNumber: echoPort}
	if err := host.AddPort(ctx, port); err != nil {
		t.Fatalf("Host.AddPort failed: %v", err)
	}
	t.Logf("Added port %d", echoPort)

	// 4. Verify the endpoint was registered.
	verifyTunnel, err := mgr.GetTunnel(ctx, tunnel, nil)
	if err != nil {
		t.Fatalf("GetTunnel (verify) failed: %v", err)
	}

	if len(verifyTunnel.Endpoints) == 0 {
		t.Fatal("expected at least one endpoint after host connect")
	}
	t.Logf("Verified %d endpoint(s) registered", len(verifyTunnel.Endpoints))

	// 5. Close the host and verify cleanup.
	if err := host.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}
	t.Log("Host closed successfully")
}

// TestHostAndClientE2ELiveRelay validates the full tunnel lifecycle with both
// a host and a client connecting through the live relay service.
// Skipped in short mode. Requires TUNNEL_AUTH_TOKEN env var.
func TestHostAndClientE2ELiveRelay(t *testing.T) {
	if testing.Short() {
		t.Skip("skipping live relay test in short mode")
	}

	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Second)
	defer cancel()

	mgr := newTestManagerForE2E(t)
	logger := log.New(os.Stdout, "e2e-full: ", log.LstdFlags)

	// 1. Create a tunnel with host and connect access tokens.
	tunnel, err := mgr.CreateTunnel(ctx, &tunnels.Tunnel{}, nil)
	if err != nil {
		t.Fatalf("CreateTunnel failed: %v", err)
	}
	t.Logf("Created tunnel: %s (cluster: %s)", tunnel.TunnelID, tunnel.ClusterID)

	defer func() {
		cleanupCtx := context.Background()
		if err := mgr.DeleteTunnel(cleanupCtx, tunnel, nil); err != nil {
			t.Logf("Warning: DeleteTunnel failed: %v", err)
		}
	}()

	// Request both host and connect tokens.
	tokenOptions := &tunnels.TunnelRequestOptions{
		TokenScopes: []tunnels.TunnelAccessScope{
			tunnels.TunnelAccessScopeHost,
			tunnels.TunnelAccessScopeConnect,
		},
	}
	tunnel, err = mgr.GetTunnel(ctx, tunnel, tokenOptions)
	if err != nil {
		t.Fatalf("GetTunnel (with tokens) failed: %v", err)
	}

	// 2. Start a local echo server.
	echoListener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("failed to start echo server: %v", err)
	}
	defer echoListener.Close()

	echoPort := uint16(echoListener.Addr().(*net.TCPAddr).Port)
	go func() {
		for {
			conn, err := echoListener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer conn.Close()
				io.Copy(conn, conn)
			}()
		}
	}()

	// 3. Host connects and adds the echo port.
	host, err := tunnels.NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()
	t.Log("Host connected")

	port := &tunnels.TunnelPort{PortNumber: echoPort}
	if err := host.AddPort(ctx, port); err != nil {
		t.Fatalf("Host.AddPort failed: %v", err)
	}
	t.Logf("Host added port %d", echoPort)

	// 4. Client connects to the same tunnel.
	// Re-fetch the tunnel to get updated endpoints and connect token.
	connectOptions := &tunnels.TunnelRequestOptions{
		TokenScopes:  []tunnels.TunnelAccessScope{tunnels.TunnelAccessScopeConnect},
		IncludePorts: true,
	}
	clientTunnel, err := mgr.GetTunnel(ctx, tunnel, connectOptions)
	if err != nil {
		t.Fatalf("GetTunnel (for client) failed: %v", err)
	}

	client, err := tunnels.NewClient(logger, clientTunnel, true)
	if err != nil {
		t.Fatalf("NewClient failed: %v", err)
	}

	if err := client.Connect(ctx, ""); err != nil {
		t.Fatalf("Client.Connect failed: %v", err)
	}
	t.Log("Client connected")

	// 5. Client waits for the forwarded port.
	if err := client.WaitForForwardedPort(ctx, echoPort); err != nil {
		t.Fatalf("WaitForForwardedPort failed: %v", err)
	}
	t.Logf("Client received forwarded port %d", echoPort)

	// 6. Client opens a connection to the forwarded port.
	listener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("failed to create listener: %v", err)
	}
	defer listener.Close()

	if err := client.ConnectListenerToForwardedPort(ctx, listener.(*net.TCPListener), echoPort); err != nil {
		t.Fatalf("ConnectListenerToForwardedPort failed: %v", err)
	}

	// Connect to the local listener to trigger port forwarding.
	localConn, err := net.DialTimeout("tcp", listener.Addr().String(), 5*time.Second)
	if err != nil {
		t.Fatalf("failed to connect to local listener: %v", err)
	}
	defer localConn.Close()

	// 7. Send test data and verify echo.
	testData := []byte("hello e2e tunnel")
	_, err = localConn.Write(testData)
	if err != nil {
		t.Fatalf("failed to write: %v", err)
	}

	buf := make([]byte, len(testData))
	localConn.SetReadDeadline(time.Now().Add(10 * time.Second))
	_, err = io.ReadFull(localConn, buf)
	if err != nil {
		t.Fatalf("failed to read echo: %v", err)
	}

	if string(buf) != string(testData) {
		t.Fatalf("data mismatch: sent %q, received %q", testData, buf)
	}

	t.Log("E2E tunnel data integrity verified")
}
