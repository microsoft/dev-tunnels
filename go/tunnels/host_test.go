// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"encoding/json"
	"errors"
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"os"
	"strings"
	"sync"
	"testing"
	"time"

	tunnelssh "github.com/microsoft/dev-tunnels/go/tunnels/ssh"
	tunnelstest "github.com/microsoft/dev-tunnels/go/tunnels/test"
	"golang.org/x/crypto/ssh"
)

func newTestManager() *Manager {
	return &Manager{
		tokenProvider: func() string { return "" },
	}
}

func TestNewHostReturnsErrWhenManagerIsNil(t *testing.T) {
	_, err := NewHost(nil, nil)
	if !errors.Is(err, ErrNoManager) {
		t.Fatalf("expected ErrNoManager, got %v", err)
	}
}

func TestNewHostGeneratesUniqueHostID(t *testing.T) {
	mgr := newTestManager()
	h1, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	h2, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if h1.hostID == h2.hostID {
		t.Fatalf("expected different hostIDs, got %s and %s", h1.hostID, h2.hostID)
	}
}

func TestNewHostGeneratesValidHostKey(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if h.hostKey == nil {
		t.Fatal("expected non-nil host key")
	}

	// Verify the key can sign and verify.
	pubKey := h.hostKey.PublicKey()
	if pubKey == nil {
		t.Fatal("expected non-nil public key")
	}

	// Verify it's an Ed25519 key.
	if pubKey.Type() != ssh.KeyAlgoED25519 {
		t.Fatalf("expected Ed25519 key, got %s", pubKey.Type())
	}

	// Verify sign/verify works.
	data := []byte("test data")
	sig, err := h.hostKey.Sign(nil, data)
	if err != nil {
		t.Fatalf("failed to sign: %v", err)
	}

	// Parse the SSH public key to get the underlying ed25519 key for verification.
	parsedKey, err := ssh.ParsePublicKey(pubKey.Marshal())
	if err != nil {
		t.Fatalf("failed to parse public key: %v", err)
	}

	if err := parsedKey.Verify(data, sig); err != nil {
		t.Fatalf("signature verification failed: %v", err)
	}

	// Verify the underlying key type with comma-ok assertion.
	cryptoKey, ok := parsedKey.(ssh.CryptoPublicKey)
	if !ok {
		t.Fatal("expected ssh.CryptoPublicKey")
	}
	cryptoPubKey := cryptoKey.CryptoPublicKey()
	if _, ok := cryptoPubKey.(ed25519.PublicKey); !ok {
		t.Fatalf("expected ed25519.PublicKey, got %T", cryptoPubKey)
	}
}

func TestNewHostSetsEndpointID(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	expected := h.hostID + "-relay"
	if h.endpointID != expected {
		t.Fatalf("expected endpointID %q, got %q", expected, h.endpointID)
	}
}

func TestHostConnectReturnsErrWhenAlreadyConnected(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Simulate being connected by setting the ssh field.
	h.ssh = &tunnelssh.HostSSHSession{}

	err = h.Connect(context.Background(), &Tunnel{})
	if !errors.Is(err, ErrAlreadyConnected) {
		t.Fatalf("expected ErrAlreadyConnected, got %v", err)
	}
}

func TestHostWaitReturnsErrWhenNotConnected(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	err = h.Wait()
	if !errors.Is(err, ErrNotConnected) {
		t.Fatalf("expected ErrNotConnected, got %v", err)
	}
}

func TestHostCloseReturnsErrWhenNotConnected(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	err = h.Close()
	if !errors.Is(err, ErrNotConnected) {
		t.Fatalf("expected ErrNotConnected, got %v", err)
	}
}

// newMockManagementAPI creates an httptest.Server that mocks the tunnel management API.
// It handles UpdateTunnelEndpoint (PUT) and DeleteTunnelEndpoints (DELETE).
// The hostRelayURI is returned in the endpoint response.
func newMockManagementAPI(t *testing.T, hostRelayURI string) (*httptest.Server, *Manager) {
	t.Helper()

	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Handle UpdateTunnelEndpoint (PUT /tunnels/{id}/endpoints/{endpointId})
		if r.Method == http.MethodPut && strings.Contains(r.URL.Path, "/endpoints/") {
			endpoint := TunnelEndpoint{
				ID: "test-endpoint",
				TunnelRelayTunnelEndpoint: TunnelRelayTunnelEndpoint{
					HostRelayURI: hostRelayURI,
				},
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(endpoint)
			return
		}

		// Handle DeleteTunnelEndpoints (DELETE /tunnels/{id}/endpoints/{endpointId})
		if r.Method == http.MethodDelete && strings.Contains(r.URL.Path, "/endpoints/") {
			w.WriteHeader(http.StatusOK)
			return
		}

		// Handle GetTunnel (GET /tunnels/{id})
		if r.Method == http.MethodGet && strings.Contains(r.URL.Path, "/tunnels/") && !strings.Contains(r.URL.Path, "/ports/") {
			tunnel := Tunnel{
				Name: "test-tunnel",
				Ports: []TunnelPort{
					{PortNumber: 8080},
				},
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(tunnel)
			return
		}

		// Handle CreateTunnelPort (PUT /tunnels/{id}/ports/{portNumber})
		if r.Method == http.MethodPut && strings.Contains(r.URL.Path, "/ports/") {
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(TunnelPort{PortNumber: 8080})
			return
		}

		// Handle DeleteTunnelPort (DELETE /tunnels/{id}/ports/{portNumber})
		if r.Method == http.MethodDelete && strings.Contains(r.URL.Path, "/ports/") {
			w.WriteHeader(http.StatusOK)
			return
		}

		w.WriteHeader(http.StatusNotFound)
	}))

	t.Cleanup(server.Close)

	serviceURL, err := url.Parse(server.URL)
	if err != nil {
		t.Fatalf("failed to parse mock server URL: %v", err)
	}

	mgr := &Manager{
		tokenProvider: func() string { return "" },
		httpClient:    &http.Client{},
		uri:           serviceURL,
		userAgents:    []UserAgent{{Name: "test", Version: "1.0"}},
		apiVersion:    "2023-09-27-preview",
	}

	return server, mgr
}

func newTestTunnel() *Tunnel {
	return &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}
}

func TestHostConnectSuccessful(t *testing.T) {
	// Start mock relay server.
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	// Create mock management API that returns the relay URL.
	_, mgr := newMockManagementAPI(t, relayServer.URL())

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer h.Close()

	// Verify the relay received the connection.
	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Verify connection status is Connected.
	if h.ConnectionStatus() != ConnectionStatusConnected {
		t.Fatalf("expected ConnectionStatusConnected, got %v", h.ConnectionStatus())
	}
}

func TestHostConnectRejectsInvalidToken(t *testing.T) {
	// Relay expects a specific token.
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel correct-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	// Management API returns the relay URL.
	_, mgr := newMockManagementAPI(t, relayServer.URL())

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Tunnel has wrong token.
	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "wrong-token",
		},
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	err = h.Connect(ctx, tunnel)
	if err == nil {
		h.Close()
		t.Fatal("expected error from Host.Connect with invalid token, got nil")
	}
}

func TestHostConnectHandlesRelayDisconnect(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}

	_, mgr := newMockManagementAPI(t, relayServer.URL())

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Close the relay server to simulate disconnect.
	relayServer.Close()

	// Wait should return (not hang forever).
	done := make(chan error, 1)
	go func() {
		done <- h.Wait()
	}()

	select {
	case <-done:
		// Wait returned, which is what we expect.
	case <-time.After(5 * time.Second):
		t.Fatal("Host.Wait did not return after relay disconnect")
	}
}

func TestHostCloseDeletesEndpoint(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	// Track whether DELETE endpoint was called.
	deleteEndpointCalled := make(chan struct{}, 1)
	mgmtServer := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method == http.MethodPut && strings.Contains(r.URL.Path, "/endpoints/") {
			endpoint := TunnelEndpoint{
				ID: "test-endpoint",
				TunnelRelayTunnelEndpoint: TunnelRelayTunnelEndpoint{
					HostRelayURI: relayServer.URL(),
				},
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(endpoint)
			return
		}
		if r.Method == http.MethodDelete && strings.Contains(r.URL.Path, "/endpoints/") {
			select {
			case deleteEndpointCalled <- struct{}{}:
			default:
			}
			w.WriteHeader(http.StatusOK)
			return
		}
		w.WriteHeader(http.StatusNotFound)
	}))
	defer mgmtServer.Close()

	serviceURL, _ := url.Parse(mgmtServer.URL)
	mgr := &Manager{
		tokenProvider: func() string { return "" },
		httpClient:    &http.Client{},
		uri:           serviceURL,
		userAgents:    []UserAgent{{Name: "test", Version: "1.0"}},
		apiVersion:    "2023-09-27-preview",
	}

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Close the host.
	if err := h.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	// Verify DeleteTunnelEndpoints was called.
	select {
	case <-deleteEndpointCalled:
		// Good, endpoint was deleted.
	case <-time.After(5 * time.Second):
		t.Fatal("Host.Close did not call DeleteTunnelEndpoints")
	}
}

func TestHostCloseIsIdempotent(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	_, mgr := newMockManagementAPI(t, relayServer.URL())

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// First close should succeed.
	if err := h.Close(); err != nil {
		t.Fatalf("first Host.Close failed: %v", err)
	}

	// Second close should succeed (idempotent), not return ErrNotConnected.
	if err := h.Close(); err != nil {
		t.Fatalf("second Host.Close should be idempotent, got: %v", err)
	}
}

func TestHostConnectionStatusCallback(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	_, mgr := newMockManagementAPI(t, relayServer.URL())

	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	var mu sync.Mutex
	var transitions []ConnectionStatus
	h.ConnectionStatusChanged = func(prev, curr ConnectionStatus) {
		mu.Lock()
		transitions = append(transitions, curr)
		mu.Unlock()
	}

	// Verify initial status is None.
	if h.ConnectionStatus() != ConnectionStatusNone {
		t.Fatalf("expected ConnectionStatusNone, got %v", h.ConnectionStatus())
	}

	// Connect triggers Connecting -> Connected.
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Close triggers Disconnected.
	if err := h.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	mu.Lock()
	defer mu.Unlock()

	expected := []ConnectionStatus{
		ConnectionStatusConnecting,
		ConnectionStatusConnected,
		ConnectionStatusDisconnected,
	}
	if len(transitions) != len(expected) {
		t.Fatalf("expected %d transitions, got %d: %v", len(expected), len(transitions), transitions)
	}
	for i, s := range expected {
		if transitions[i] != s {
			t.Fatalf("transition[%d]: expected %v, got %v", i, s, transitions[i])
		}
	}
}

func TestHostTooManyConnectionsGuard(t *testing.T) {
	mgr := newTestManager()
	h, err := NewHost(nil, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Simulate a prior TooManyConnections disconnect.
	h.disconnectReason = tunnelssh.SshDisconnectReasonTooManyConnections

	err = h.Connect(context.Background(), &Tunnel{})
	if !errors.Is(err, ErrTooManyConnections) {
		t.Fatalf("expected ErrTooManyConnections, got %v", err)
	}
}

// tcpConnPair creates a pair of connected net.Conn via TCP loopback.
// Unlike net.Pipe(), TCP connections have kernel buffering so both sides
// can write concurrently without deadlocking during SSH handshakes.
func tcpConnPair(t *testing.T) (net.Conn, net.Conn) {
	t.Helper()
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("failed to listen: %v", err)
	}
	defer ln.Close()

	var serverConn net.Conn
	var serverErr error
	done := make(chan struct{})
	go func() {
		defer close(done)
		serverConn, serverErr = ln.Accept()
	}()

	clientConn, err := net.Dial("tcp", ln.Addr().String())
	if err != nil {
		t.Fatalf("failed to dial: %v", err)
	}
	<-done
	if serverErr != nil {
		clientConn.Close()
		t.Fatalf("failed to accept: %v", serverErr)
	}
	return clientConn, serverConn
}

func TestHostAndClientIntegration(t *testing.T) {
	// 1. Start a TCP echo server on a random localhost port.
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

	// 2. Create TCP loopback pair for host-relay connection.
	hostEnd, relayEnd := tcpConnPair(t)

	// 3. Generate a host key.
	_, privKey, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		t.Fatalf("failed to generate host key: %v", err)
	}
	hostKey, err := ssh.NewSignerFromKey(privKey)
	if err != nil {
		t.Fatalf("failed to create signer: %v", err)
	}

	logger := log.New(os.Stderr, "e2e-test: ", log.LstdFlags)

	// 4. Create HostSSHSession (V2) and connect concurrently with the mock relay.
	session := tunnelssh.NewHostSSHSession(hostEnd, hostKey, logger, "test-token", tunnelssh.HostWebSocketSubProtocolV2)

	var relay *tunnelstest.MockRelayForHost
	var relayErr error
	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		defer wg.Done()
		relay, relayErr = tunnelstest.NewMockRelayForHost(relayEnd)
	}()

	ctx := context.Background()
	if err := session.Connect(ctx); err != nil {
		t.Fatalf("failed to connect host session: %v", err)
	}
	wg.Wait()
	if relayErr != nil {
		t.Fatalf("relay SSH handshake failed: %v", relayErr)
	}
	defer func() {
		session.Close()
		relay.Close()
	}()

	// 5. Add the echo port to the host — sends tcpip-forward to relay.
	session.AddPort(echoPort, "test-token")

	// Give the relay time to process the tcpip-forward request.
	time.Sleep(200 * time.Millisecond)

	// Verify relay registered the port.
	if !relay.HasPort(echoPort) {
		t.Fatalf("relay did not register port %d", echoPort)
	}

	// 6. Simulate a client connection via the V2 mock relay.
	clientConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("failed to simulate client connection: %v", err)
	}

	// 7. In V2, the channel IS the data stream — no nested SSH.
	// Send 'hello tunnel' through the tunnel and verify echo response.
	testData := []byte("hello tunnel")
	_, err = clientConn.Write(testData)
	if err != nil {
		t.Fatalf("failed to write through tunnel: %v", err)
	}

	buf := make([]byte, len(testData))
	_, err = io.ReadFull(clientConn, buf)
	if err != nil {
		t.Fatalf("failed to read echo response: %v", err)
	}

	if string(buf) != string(testData) {
		t.Fatalf("data integrity check failed: sent %q, received %q", testData, buf)
	}

	clientConn.Close()
}

func TestHostRefreshPorts(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	// Mock management API that returns different ports on GET vs what the host has locally.
	remotePorts := []TunnelPort{
		{PortNumber: 3000},
		{PortNumber: 4000},
	}
	mgmtServer := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// UpdateTunnelEndpoint
		if r.Method == http.MethodPut && strings.Contains(r.URL.Path, "/endpoints/") {
			endpoint := TunnelEndpoint{
				ID: "test-endpoint",
				TunnelRelayTunnelEndpoint: TunnelRelayTunnelEndpoint{
					HostRelayURI: relayServer.URL(),
				},
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(endpoint)
			return
		}
		// DeleteTunnelEndpoints
		if r.Method == http.MethodDelete && strings.Contains(r.URL.Path, "/endpoints/") {
			w.WriteHeader(http.StatusOK)
			return
		}
		// GetTunnel — returns the remote ports.
		if r.Method == http.MethodGet && strings.Contains(r.URL.Path, "/tunnels/") {
			tunnel := Tunnel{
				Name:  "test-tunnel",
				Ports: remotePorts,
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(tunnel)
			return
		}
		w.WriteHeader(http.StatusNotFound)
	}))
	defer mgmtServer.Close()

	serviceURL, _ := url.Parse(mgmtServer.URL)
	mgr := &Manager{
		tokenProvider: func() string { return "" },
		httpClient:    &http.Client{},
		uri:           serviceURL,
		userAgents:    []UserAgent{{Name: "test", Version: "1.0"}},
		apiVersion:    "2023-09-27-preview",
	}

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer h.Close()

	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Locally add port 5000 via the SSH session (not on remote).
	h.mu.Lock()
	sshSession := h.ssh
	h.mu.Unlock()
	sshSession.AddPort(5000, "test-token")

	// Call RefreshPorts — should add 3000, 4000 and remove 5000.
	if err := h.RefreshPorts(ctx); err != nil {
		t.Fatalf("RefreshPorts failed: %v", err)
	}

	if !sshSession.HasPort(3000) {
		t.Fatal("expected port 3000 to be added by RefreshPorts")
	}
	if !sshSession.HasPort(4000) {
		t.Fatal("expected port 4000 to be added by RefreshPorts")
	}
	if sshSession.HasPort(5000) {
		t.Fatal("expected port 5000 to be removed by RefreshPorts")
	}
}

func TestHostConnectNegotiatesProtocol(t *testing.T) {
	// Relay forces V1 protocol.
	relayServer, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
		tunnelstest.WithProtocolV1Only(),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	defer relayServer.Close()

	_, mgr := newMockManagementAPI(t, relayServer.URL())

	logger := log.New(os.Stderr, "test: ", log.LstdFlags)
	h, err := NewHost(logger, mgr)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := newTestTunnel()
	if err := h.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer h.Close()

	if err := relayServer.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Verify the relay negotiated V1.
	if relayServer.NegotiatedProtocol() != "tunnel-relay-host" {
		t.Fatalf("expected V1 protocol, got %q", relayServer.NegotiatedProtocol())
	}

	// Verify the host session knows it's V1.
	h.mu.Lock()
	sshSession := h.ssh
	h.mu.Unlock()

	if sshSession.ConnectionProtocol() != tunnelssh.HostWebSocketSubProtocol {
		t.Fatalf("expected host session protocol %q, got %q",
			tunnelssh.HostWebSocketSubProtocol, sshSession.ConnectionProtocol())
	}
}

func TestHostAndClientIntegrationV1(t *testing.T) {
	// 1. Start a TCP echo server on a random localhost port.
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

	// 2. Create TCP loopback pair for host-relay connection.
	hostEnd, relayEnd := tcpConnPair(t)

	// 3. Generate a host key.
	_, privKey, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		t.Fatalf("failed to generate host key: %v", err)
	}
	hostKey, err := ssh.NewSignerFromKey(privKey)
	if err != nil {
		t.Fatalf("failed to create signer: %v", err)
	}

	logger := log.New(os.Stderr, "e2e-v1-test: ", log.LstdFlags)

	// 4. Create HostSSHSession (V1) and connect concurrently with the V1 mock relay.
	session := tunnelssh.NewHostSSHSession(hostEnd, hostKey, logger, "", tunnelssh.HostWebSocketSubProtocol)

	var relay *tunnelstest.MockRelayForHostV1
	var relayErr error
	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		defer wg.Done()
		relay, relayErr = tunnelstest.NewMockRelayForHostV1(relayEnd)
	}()

	ctx := context.Background()
	if err := session.Connect(ctx); err != nil {
		t.Fatalf("failed to connect host session: %v", err)
	}
	wg.Wait()
	if relayErr != nil {
		t.Fatalf("relay SSH handshake failed: %v", relayErr)
	}
	defer func() {
		session.Close()
		relay.Close()
	}()

	// 5. Add the echo port to the host.
	session.AddPort(echoPort, "")

	// 6. Simulate a V1 client connection via the mock relay.
	client, err := relay.SimulateClientConnection()
	if err != nil {
		t.Fatalf("failed to simulate V1 client connection: %v", err)
	}
	defer client.Close()

	// Give time for port forward notifications.
	time.Sleep(500 * time.Millisecond)

	// 7. Open a forwarded-tcpip channel from the client to the host.
	// In V1, the client opens direct-tcpip to the host's nested SSH server.
	ch, reqs, err := client.Conn.OpenChannel("direct-tcpip", ssh.Marshal(struct {
		Host       string
		Port       uint32
		OriginAddr string
		OriginPort uint32
	}{
		Host:       "127.0.0.1",
		Port:       uint32(echoPort),
		OriginAddr: "127.0.0.1",
		OriginPort: 0,
	}))
	if err != nil {
		t.Fatalf("failed to open direct-tcpip channel: %v", err)
	}
	go ssh.DiscardRequests(reqs)

	// 8. Send data through the tunnel and verify echo.
	testData := []byte("hello v1 tunnel")
	_, err = ch.Write(testData)
	if err != nil {
		t.Fatalf("failed to write through V1 tunnel: %v", err)
	}

	buf := make([]byte, len(testData))
	_, err = io.ReadFull(ch, buf)
	if err != nil {
		t.Fatalf("failed to read echo response: %v", err)
	}

	if string(buf) != string(testData) {
		t.Fatalf("V1 data integrity check failed: sent %q, received %q", testData, buf)
	}

	ch.Close()
}
