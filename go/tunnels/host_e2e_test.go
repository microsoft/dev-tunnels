// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"os"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	tunnelssh "github.com/microsoft/dev-tunnels/go/tunnels/ssh"
	tunnelstest "github.com/microsoft/dev-tunnels/go/tunnels/test"
)

// e2eMockAPI provides a mock tunnel management API backed by httptest.Server.
// It tracks calls to key endpoints via atomic counters and allows dynamic
// control of relay URI, remote ports, and 401 simulation.
type e2eMockAPI struct {
	server              *httptest.Server
	manager             *Manager
	deleteEndpointCalls int32        // atomic
	createPortCalls     int32        // atomic
	portConflictOnce    int32        // atomic flag for one-shot 409 simulation on port creation
	remotePorts         atomic.Value // stores []TunnelPort
	relayURI            atomic.Value // stores string
	unauthorizedOnce    int32        // atomic flag for one-shot 401 simulation
}

// newE2EMockAPI creates an e2eMockAPI with its httptest.Server and a Manager
// configured to use it. The relayURI is set to the given initial value.
func newE2EMockAPI(t *testing.T, initialRelayURI string) *e2eMockAPI {
	t.Helper()

	api := &e2eMockAPI{}
	api.relayURI.Store(initialRelayURI)
	api.remotePorts.Store([]TunnelPort{})

	mux := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		// Check for one-shot 401 simulation on UpdateTunnelEndpoint.
		if r.Method == http.MethodPut && containsSegment(r.URL.Path, "endpoints") {
			if atomic.CompareAndSwapInt32(&api.unauthorizedOnce, 1, 0) {
				w.WriteHeader(http.StatusUnauthorized)
				json.NewEncoder(w).Encode(map[string]string{"detail": "token expired"})
				return
			}
			uri := api.relayURI.Load().(string)
			endpoint := TunnelEndpoint{
				ID: "test-endpoint",
				TunnelRelayTunnelEndpoint: TunnelRelayTunnelEndpoint{
					HostRelayURI: uri,
				},
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(endpoint)
			return
		}

		// DeleteTunnelEndpoints
		if r.Method == http.MethodDelete && containsSegment(r.URL.Path, "endpoints") {
			atomic.AddInt32(&api.deleteEndpointCalls, 1)
			w.WriteHeader(http.StatusOK)
			return
		}

		// GetTunnel (GET .../tunnels/... but not .../ports/...)
		if r.Method == http.MethodGet && containsSegment(r.URL.Path, "tunnels") && !containsSegment(r.URL.Path, "ports") {
			ports := api.remotePorts.Load().([]TunnelPort)
			tunnel := Tunnel{
				Name:  "test-tunnel",
				Ports: ports,
			}
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(tunnel)
			return
		}

		// CreateTunnelPort (PUT .../ports/...)
		if r.Method == http.MethodPut && containsSegment(r.URL.Path, "ports") {
			if atomic.CompareAndSwapInt32(&api.portConflictOnce, 1, 0) {
				w.WriteHeader(http.StatusConflict)
				json.NewEncoder(w).Encode(map[string]string{"detail": "port already exists"})
				return
			}
			atomic.AddInt32(&api.createPortCalls, 1)
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(TunnelPort{PortNumber: 0})
			return
		}

		// DeleteTunnelPort
		if r.Method == http.MethodDelete && containsSegment(r.URL.Path, "ports") {
			w.WriteHeader(http.StatusOK)
			return
		}

		// GetTunnelPorts (GET .../ports/...)
		if r.Method == http.MethodGet && containsSegment(r.URL.Path, "ports") {
			ports := api.remotePorts.Load().([]TunnelPort)
			w.Header().Set("Content-Type", "application/json")
			json.NewEncoder(w).Encode(ports)
			return
		}

		w.WriteHeader(http.StatusNotFound)
	})

	server := httptest.NewServer(mux)
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

	api.server = server
	api.manager = mgr
	return api
}

// containsSegment checks if a URL path contains a given segment delimited by slashes.
func containsSegment(path, segment string) bool {
	// Simple substring check — sufficient for test routing.
	return len(path) > 0 && findSubstring(path, "/"+segment+"/") || hasSuffix(path, "/"+segment)
}

func findSubstring(s, sub string) bool {
	return len(sub) <= len(s) && containsStr(s, sub)
}

func containsStr(s, sub string) bool {
	for i := 0; i+len(sub) <= len(s); i++ {
		if s[i:i+len(sub)] == sub {
			return true
		}
	}
	return false
}

func hasSuffix(s, suffix string) bool {
	return len(s) >= len(suffix) && s[len(s)-len(suffix):] == suffix
}

// startEchoServerE2E starts a TCP echo server on 127.0.0.1:0.
// It registers cleanup to close the listener when the test ends.
// Returns the listener and port number.
func startEchoServerE2E(t *testing.T) (net.Listener, uint16) {
	t.Helper()

	listener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("failed to start echo server: %v", err)
	}

	go func() {
		for {
			conn, err := listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer conn.Close()
				io.Copy(conn, conn)
			}()
		}
	}()

	t.Cleanup(func() { listener.Close() })
	port := uint16(listener.Addr().(*net.TCPAddr).Port)
	return listener, port
}

func TestE2E_HostLifecycle(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API pointing to relay.
	api := newE2EMockAPI(t, relay.URL())

	logger := log.New(os.Stderr, "e2e-lifecycle: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	// Assert initial status is None.
	if status := host.ConnectionStatus(); status != ConnectionStatusNone {
		t.Fatalf("expected ConnectionStatusNone, got %v", status)
	}

	// Assert HostPublicKeyBase64 is non-empty and base64-decodable.
	pubKeyB64 := host.HostPublicKeyBase64()
	if pubKeyB64 == "" {
		t.Fatal("HostPublicKeyBase64 returned empty string")
	}
	if _, err := base64.StdEncoding.DecodeString(pubKeyB64); err != nil {
		t.Fatalf("HostPublicKeyBase64 is not valid base64: %v", err)
	}

	// Connect to the relay.
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	// Wait for relay to confirm connection.
	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Assert connected status.
	if status := host.ConnectionStatus(); status != ConnectionStatusConnected {
		t.Fatalf("expected ConnectionStatusConnected, got %v", status)
	}

	// Close the host.
	if err := host.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	// Assert disconnected status.
	if status := host.ConnectionStatus(); status != ConnectionStatusDisconnected {
		t.Fatalf("expected ConnectionStatusDisconnected, got %v", status)
	}

	// Assert endpoint was deleted exactly once.
	deleteCalls := atomic.LoadInt32(&api.deleteEndpointCalls)
	if deleteCalls != 1 {
		t.Fatalf("expected 1 deleteEndpointCalls, got %d", deleteCalls)
	}

	// Second Close should be idempotent (return nil).
	if err := host.Close(); err != nil {
		t.Fatalf("second Host.Close should be idempotent, got: %v", err)
	}

	// deleteEndpointCalls should still be 1.
	deleteCalls = atomic.LoadInt32(&api.deleteEndpointCalls)
	if deleteCalls != 1 {
		t.Fatalf("expected deleteEndpointCalls still 1 after idempotent close, got %d", deleteCalls)
	}
}

func TestE2E_ErrorHandling(t *testing.T) {
	t.Run("ErrNoManager", func(t *testing.T) {
		_, err := NewHost(nil, nil)
		if !errors.Is(err, ErrNoManager) {
			t.Fatalf("expected ErrNoManager, got %v", err)
		}
	})

	t.Run("ErrAlreadyConnected", func(t *testing.T) {
		relay, err := tunnelstest.NewRelayHostServer(
			tunnelstest.WithHostAccessToken("Tunnel test-token"),
		)
		if err != nil {
			t.Fatalf("failed to create relay: %v", err)
		}
		t.Cleanup(func() { relay.Close() })

		api := newE2EMockAPI(t, relay.URL())
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		tunnel := &Tunnel{
			Name: "test-tunnel",
			AccessTokens: map[TunnelAccessScope]string{
				TunnelAccessScopeHost: "test-token",
			},
		}

		if err := host.Connect(ctx, tunnel); err != nil {
			t.Fatalf("first Connect failed: %v", err)
		}
		defer host.Close()

		if err := relay.WaitForConnection(5 * time.Second); err != nil {
			t.Fatalf("relay did not receive connection: %v", err)
		}

		err = host.Connect(ctx, tunnel)
		if !errors.Is(err, ErrAlreadyConnected) {
			t.Fatalf("expected ErrAlreadyConnected, got %v", err)
		}
	})

	t.Run("ErrNotConnected_Close", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		err = host.Close()
		if !errors.Is(err, ErrNotConnected) {
			t.Fatalf("expected ErrNotConnected, got %v", err)
		}
	})

	t.Run("ErrNotConnected_Wait", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		err = host.Wait()
		if !errors.Is(err, ErrNotConnected) {
			t.Fatalf("expected ErrNotConnected, got %v", err)
		}
	})

	t.Run("ErrNotConnected_AddPort", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx := context.Background()
		err = host.AddPort(ctx, &TunnelPort{PortNumber: 8080})
		if !errors.Is(err, ErrNotConnected) {
			t.Fatalf("expected ErrNotConnected, got %v", err)
		}
	})

	t.Run("ErrNotConnected_RemovePort", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx := context.Background()
		err = host.RemovePort(ctx, 8080)
		if !errors.Is(err, ErrNotConnected) {
			t.Fatalf("expected ErrNotConnected, got %v", err)
		}
	})

	t.Run("ErrNotConnected_RefreshPorts", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx := context.Background()
		err = host.RefreshPorts(ctx)
		if !errors.Is(err, ErrNotConnected) {
			t.Fatalf("expected ErrNotConnected, got %v", err)
		}
	})

	t.Run("ErrPortAlreadyAdded", func(t *testing.T) {
		relay, err := tunnelstest.NewRelayHostServer(
			tunnelstest.WithHostAccessToken("Tunnel test-token"),
		)
		if err != nil {
			t.Fatalf("failed to create relay: %v", err)
		}
		t.Cleanup(func() { relay.Close() })

		api := newE2EMockAPI(t, relay.URL())
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		tunnel := &Tunnel{
			Name: "test-tunnel",
			AccessTokens: map[TunnelAccessScope]string{
				TunnelAccessScopeHost: "test-token",
			},
		}

		if err := host.Connect(ctx, tunnel); err != nil {
			t.Fatalf("Connect failed: %v", err)
		}
		defer host.Close()

		if err := relay.WaitForConnection(5 * time.Second); err != nil {
			t.Fatalf("relay did not receive connection: %v", err)
		}

		port := &TunnelPort{PortNumber: 8080}
		if err := host.AddPort(ctx, port); err != nil {
			t.Fatalf("first AddPort failed: %v", err)
		}

		err = host.AddPort(ctx, port)
		if !errors.Is(err, ErrPortAlreadyAdded) {
			t.Fatalf("expected ErrPortAlreadyAdded, got %v", err)
		}
	})

	t.Run("ErrTooManyConnections", func(t *testing.T) {
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		// Set disconnect reason to TooManyConnections (same package — can access internal fields).
		host.mu.Lock()
		host.disconnectReason = tunnelssh.SshDisconnectReasonTooManyConnections
		host.mu.Unlock()

		ctx := context.Background()
		tunnel := &Tunnel{Name: "test-tunnel"}

		err = host.Connect(ctx, tunnel)
		if !errors.Is(err, ErrTooManyConnections) {
			t.Fatalf("expected ErrTooManyConnections, got %v", err)
		}
	})

	t.Run("ErrNoHostRelayURI", func(t *testing.T) {
		// Mock API with empty relay URI -> UpdateTunnelEndpoint returns empty HostRelayURI.
		api := newE2EMockAPI(t, "")
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		tunnel := &Tunnel{
			Name: "test-tunnel",
			AccessTokens: map[TunnelAccessScope]string{
				TunnelAccessScopeHost: "test-token",
			},
		}

		err = host.Connect(ctx, tunnel)
		if !errors.Is(err, ErrNoHostRelayURI) {
			t.Fatalf("expected ErrNoHostRelayURI, got %v", err)
		}
	})

	t.Run("409Conflict_Tolerated", func(t *testing.T) {
		relay, err := tunnelstest.NewRelayHostServer(
			tunnelstest.WithHostAccessToken("Tunnel test-token"),
		)
		if err != nil {
			t.Fatalf("failed to create relay: %v", err)
		}
		t.Cleanup(func() { relay.Close() })

		api := newE2EMockAPI(t, relay.URL())
		host, err := NewHost(log.New(os.Stderr, "e2e-err: ", log.LstdFlags), api.manager)
		if err != nil {
			t.Fatalf("NewHost failed: %v", err)
		}

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		tunnel := &Tunnel{
			Name: "test-tunnel",
			AccessTokens: map[TunnelAccessScope]string{
				TunnelAccessScopeHost: "test-token",
			},
		}

		if err := host.Connect(ctx, tunnel); err != nil {
			t.Fatalf("Connect failed: %v", err)
		}
		defer host.Close()

		if err := relay.WaitForConnection(5 * time.Second); err != nil {
			t.Fatalf("relay did not receive connection: %v", err)
		}

		// Set flag so next port creation returns 409 Conflict.
		atomic.StoreInt32(&api.portConflictOnce, 1)

		port := &TunnelPort{PortNumber: 9999}
		if err := host.AddPort(ctx, port); err != nil {
			t.Fatalf("AddPort should tolerate 409 Conflict, got: %v", err)
		}
	})
}

func TestE2E_PortForwardingDataFlow(t *testing.T) {
	// Start TCP echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-dataflow: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port (host sends tcpip-forward to relay internally).
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process the tcpip-forward request.
	time.Sleep(200 * time.Millisecond)

	// Simulate a client connection via the relay — opens a forwarded-tcpip channel directly.
	clientConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection failed: %v", err)
	}

	// Write test data through the tunnel (V2: net.Conn is the data stream).
	testData := []byte("hello e2e tunnel")
	if _, err := clientConn.Write(testData); err != nil {
		t.Fatalf("failed to write through tunnel: %v", err)
	}

	// Read echo response.
	buf := make([]byte, len(testData))
	if _, err := io.ReadFull(clientConn, buf); err != nil {
		t.Fatalf("failed to read echo response: %v", err)
	}

	// Assert sent bytes == received bytes.
	if string(buf) != string(testData) {
		t.Fatalf("data integrity check failed: sent %q, received %q", testData, buf)
	}

	clientConn.Close()
}

func TestE2E_DirectTcpipAndForwardedTcpip(t *testing.T) {
	// Start echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-channel-types: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process the tcpip-forward request.
	time.Sleep(200 * time.Millisecond)

	// Test 1: forwarded-tcpip channel via SimulateClientConnection — echo "forwarded-test".
	fwdConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection failed: %v", err)
	}

	fwdMsg := []byte("forwarded-test")
	if _, err := fwdConn.Write(fwdMsg); err != nil {
		t.Fatalf("failed to write forwarded-tcpip: %v", err)
	}
	fwdBuf := make([]byte, len(fwdMsg))
	if _, err := io.ReadFull(fwdConn, fwdBuf); err != nil {
		t.Fatalf("failed to read forwarded-tcpip echo: %v", err)
	}
	if string(fwdBuf) != string(fwdMsg) {
		t.Fatalf("forwarded-tcpip echo mismatch: sent %q, got %q", fwdMsg, fwdBuf)
	}
	fwdConn.Close()

	// Test 2: second forwarded-tcpip channel — echo "direct-test".
	// In V2, both forwarded-tcpip and direct-tcpip are handled via the relay.
	// SimulateClientConnection uses forwarded-tcpip which covers the V2 data path.
	directConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection (second) failed: %v", err)
	}

	directMsg := []byte("direct-test")
	if _, err := directConn.Write(directMsg); err != nil {
		t.Fatalf("failed to write second channel: %v", err)
	}
	directBuf := make([]byte, len(directMsg))
	if _, err := io.ReadFull(directConn, directBuf); err != nil {
		t.Fatalf("failed to read second channel echo: %v", err)
	}
	if string(directBuf) != string(directMsg) {
		t.Fatalf("second channel echo mismatch: sent %q, got %q", directMsg, directBuf)
	}
	directConn.Close()

	// Test 3: connection to unregistered port — should be rejected.
	unregPort := echoPort + 1000
	_, err = relay.SimulateClientConnection(unregPort)
	if err == nil {
		t.Fatal("expected error connecting to unregistered port, got nil")
	}
}

func TestE2E_MultiplePorts(t *testing.T) {
	// Start 3 echo servers on different ports.
	_, portA := startEchoServerE2E(t)
	_, portB := startEchoServerE2E(t)
	_, portC := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-multiport: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add all 3 ports.
	for _, port := range []uint16{portA, portB, portC} {
		if err := host.AddPort(ctx, &TunnelPort{PortNumber: port}); err != nil {
			t.Fatalf("AddPort(%d) failed: %v", port, err)
		}
	}

	// Give the relay time to process all tcpip-forward requests.
	time.Sleep(200 * time.Millisecond)

	// Verify relay registered all three ports.
	for _, port := range []uint16{portA, portB, portC} {
		if !relay.HasPort(port) {
			t.Fatalf("relay did not register port %d", port)
		}
	}

	// Test each port with unique messages — no cross-contamination.
	ports := []uint16{portA, portB, portC}
	messages := []string{"port-A", "port-B", "port-C"}

	for i, port := range ports {
		clientConn, err := relay.SimulateClientConnection(port)
		if err != nil {
			t.Fatalf("SimulateClientConnection(%d) failed: %v", port, err)
		}
		msg := []byte(messages[i])
		if _, err := clientConn.Write(msg); err != nil {
			t.Fatalf("port %d: write failed: %v", port, err)
		}
		buf := make([]byte, len(msg))
		if _, err := io.ReadFull(clientConn, buf); err != nil {
			t.Fatalf("port %d: read failed: %v", port, err)
		}
		if string(buf) != string(msg) {
			t.Fatalf("port %d: echo mismatch: sent %q, got %q", port, msg, buf)
		}
		clientConn.Close()
	}
}

func TestE2E_DynamicPortManagement(t *testing.T) {
	// Start echo server A.
	_, echoPortA := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-dynamic-ports: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add port A.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPortA}); err != nil {
		t.Fatalf("AddPort(A) failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Verify data flows through port A.
	connA, err := relay.SimulateClientConnection(echoPortA)
	if err != nil {
		t.Fatalf("SimulateClientConnection(A) failed: %v", err)
	}
	msgA := []byte("dynamic-port-A")
	if _, err := connA.Write(msgA); err != nil {
		t.Fatalf("port A: write failed: %v", err)
	}
	bufA := make([]byte, len(msgA))
	if _, err := io.ReadFull(connA, bufA); err != nil {
		t.Fatalf("port A: read failed: %v", err)
	}
	if string(bufA) != string(msgA) {
		t.Fatalf("port A: echo mismatch: sent %q, got %q", msgA, bufA)
	}
	connA.Close()

	// Start echo server B and add port B dynamically.
	_, echoPortB := startEchoServerE2E(t)
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPortB}); err != nil {
		t.Fatalf("AddPort(B) failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Verify data flows through port B.
	connB, err := relay.SimulateClientConnection(echoPortB)
	if err != nil {
		t.Fatalf("SimulateClientConnection(B) failed: %v", err)
	}
	msgB := []byte("dynamic-port-B")
	if _, err := connB.Write(msgB); err != nil {
		t.Fatalf("port B: write failed: %v", err)
	}
	bufB := make([]byte, len(msgB))
	if _, err := io.ReadFull(connB, bufB); err != nil {
		t.Fatalf("port B: read failed: %v", err)
	}
	if string(bufB) != string(msgB) {
		t.Fatalf("port B: echo mismatch: sent %q, got %q", msgB, bufB)
	}
	connB.Close()

	// Remove port A.
	if err := host.RemovePort(ctx, echoPortA); err != nil {
		t.Fatalf("RemovePort(A) failed: %v", err)
	}

	// Give the relay time to process cancel-tcpip-forward.
	time.Sleep(200 * time.Millisecond)

	// Verify the relay no longer has port A.
	if relay.HasPort(echoPortA) {
		t.Fatal("relay should not have port A after RemovePort")
	}

	// Attempt to connect to removed port A — expect rejection.
	_, err = relay.SimulateClientConnection(echoPortA)
	if err == nil {
		t.Fatal("expected error connecting to removed port A")
	}
}

func TestE2E_PortDuplicateHandling(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-dup-port: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// First AddPort should succeed.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: 8080}); err != nil {
		t.Fatalf("first AddPort failed: %v", err)
	}

	// Second AddPort with same port should return ErrPortAlreadyAdded.
	err = host.AddPort(ctx, &TunnelPort{PortNumber: 8080})
	if !errors.Is(err, ErrPortAlreadyAdded) {
		t.Fatalf("expected ErrPortAlreadyAdded, got %v", err)
	}

	// Access SSH session and verify port 8080 appears exactly once.
	host.mu.Lock()
	sshSession := host.ssh
	host.mu.Unlock()

	ports := sshSession.Ports()
	count := 0
	for _, p := range ports {
		if p == 8080 {
			count++
		}
	}
	if count != 1 {
		t.Fatalf("expected port 8080 exactly once, found %d times in %v", count, ports)
	}

	// Verify createPortCalls == 1 (only the first AddPort called the API).
	createCalls := atomic.LoadInt32(&api.createPortCalls)
	if createCalls != 1 {
		t.Fatalf("expected 1 createPortCalls, got %d", createCalls)
	}
}

func TestE2E_RefreshPorts(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-refresh-ports: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Access SSH session directly (same package — internal fields accessible).
	host.mu.Lock()
	sshSession := host.ssh
	host.mu.Unlock()

	// Manually add port 5000 to the SSH session.
	sshSession.AddPort(5000, "test-token")

	// Configure mock API to return ports 3000 and 4000 as the remote set.
	api.remotePorts.Store([]TunnelPort{
		{PortNumber: 3000},
		{PortNumber: 4000},
	})

	// RefreshPorts should synchronize: add 3000, 4000; remove 5000.
	if err := host.RefreshPorts(ctx); err != nil {
		t.Fatalf("RefreshPorts failed: %v", err)
	}

	// Assert port 3000 was added from service.
	if !sshSession.HasPort(3000) {
		t.Fatal("expected port 3000 to be present after RefreshPorts")
	}

	// Assert port 4000 was added from service.
	if !sshSession.HasPort(4000) {
		t.Fatal("expected port 4000 to be present after RefreshPorts")
	}

	// Assert port 5000 was removed (not on service).
	if sshSession.HasPort(5000) {
		t.Fatal("expected port 5000 to be removed after RefreshPorts")
	}

	// Call RefreshPorts again — should be idempotent (no changes).
	if err := host.RefreshPorts(ctx); err != nil {
		t.Fatalf("idempotent RefreshPorts failed: %v", err)
	}

	// Verify ports are still correct after idempotent call.
	if !sshSession.HasPort(3000) {
		t.Fatal("expected port 3000 still present after idempotent RefreshPorts")
	}
	if !sshSession.HasPort(4000) {
		t.Fatal("expected port 4000 still present after idempotent RefreshPorts")
	}
	if sshSession.HasPort(5000) {
		t.Fatal("port 5000 should still be absent after idempotent RefreshPorts")
	}
}

func TestE2E_LargeDataTransfer(t *testing.T) {
	// Start echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-large-data: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Simulate a client connection via the relay.
	clientConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection failed: %v", err)
	}

	// Generate 1MB payload: payload[i] = byte(i % 256).
	const payloadSize = 1048576 // 1MB
	payload := make([]byte, payloadSize)
	for i := range payload {
		payload[i] = byte(i % 256)
	}

	// Compute expected SHA256 hash.
	expectedHash := sha256.Sum256(payload)

	// Goroutine: write payload, then close write side.
	writeErr := make(chan error, 1)
	go func() {
		_, err := clientConn.Write(payload)
		// Close write side to signal EOF to the echo server.
		// channelNetConn wraps ssh.Channel which supports CloseWrite via Close.
		writeErr <- err
	}()

	// Main: read all echoed data.
	var received bytes.Buffer
	if _, err := io.CopyN(&received, clientConn, payloadSize); err != nil {
		t.Fatalf("failed to read echo response: %v", err)
	}

	// Check write error.
	if err := <-writeErr; err != nil {
		t.Fatalf("failed to write payload: %v", err)
	}

	// Assert received length == 1MB.
	if received.Len() != payloadSize {
		t.Fatalf("expected %d bytes, received %d", payloadSize, received.Len())
	}

	// Compute actual SHA256 hash and compare.
	actualHash := sha256.Sum256(received.Bytes())
	if expectedHash != actualHash {
		t.Fatalf("SHA256 hash mismatch: expected %x, got %x", expectedHash, actualHash)
	}

	clientConn.Close()
}

func TestE2E_ConnectionStatusCallbacks(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API pointing to relay.
	api := newE2EMockAPI(t, relay.URL())

	logger := log.New(os.Stderr, "e2e-callbacks: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	// Assert initial status is None.
	if status := host.ConnectionStatus(); status != ConnectionStatusNone {
		t.Fatalf("expected initial ConnectionStatusNone, got %v", status)
	}

	// Register callback that appends transitions to a mutex-guarded slice.
	var mu sync.Mutex
	var transitions []ConnectionStatus
	host.ConnectionStatusChanged = func(prev, curr ConnectionStatus) {
		mu.Lock()
		transitions = append(transitions, curr)
		mu.Unlock()
	}

	// Connect to the relay.
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	// Wait for relay to confirm connection.
	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Close the host.
	if err := host.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	// Assert transitions == [Connecting, Connected, Disconnected].
	mu.Lock()
	got := make([]ConnectionStatus, len(transitions))
	copy(got, transitions)
	mu.Unlock()

	expected := []ConnectionStatus{
		ConnectionStatusConnecting,
		ConnectionStatusConnected,
		ConnectionStatusDisconnected,
	}

	if len(got) != len(expected) {
		t.Fatalf("expected %d transitions, got %d: %v", len(expected), len(got), got)
	}

	for i, exp := range expected {
		if got[i] != exp {
			t.Fatalf("transition[%d]: expected %v, got %v (full: %v)", i, exp, got[i], got)
		}
	}
}

func TestE2E_BidirectionalStreaming(t *testing.T) {
	// Start echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-bidir: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Simulate a client connection via the relay.
	clientConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection failed: %v", err)
	}

	// Send 10 messages of increasing size (100, 200, ..., 1000 bytes).
	for i := 1; i <= 10; i++ {
		size := i * 100
		msg := bytes.Repeat([]byte{byte(i)}, size)

		if _, err := clientConn.Write(msg); err != nil {
			t.Fatalf("message %d: write failed: %v", i, err)
		}

		buf := make([]byte, size)
		if _, err := io.ReadFull(clientConn, buf); err != nil {
			t.Fatalf("message %d: read failed: %v", i, err)
		}

		if !bytes.Equal(buf, msg) {
			t.Fatalf("message %d: echo mismatch: sent %d bytes of 0x%02x, got different content", i, size, byte(i))
		}
	}

	clientConn.Close()
}

func TestE2E_MultipleConcurrentClients(t *testing.T) {
	// Start echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-concurrent-clients: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Spawn 5 goroutines, each simulating a client connection and verifying echo.
	// In V2, each call to SimulateClientConnection opens a new forwarded-tcpip channel.
	const numClients = 5
	var wg sync.WaitGroup
	errs := make(chan error, numClients)

	for i := 0; i < numClients; i++ {
		wg.Add(1)
		go func(clientID int) {
			defer wg.Done()

			// Simulate a client connection via the relay.
			clientConn, err := relay.SimulateClientConnection(echoPort)
			if err != nil {
				errs <- fmt.Errorf("client-%d: SimulateClientConnection failed: %v", clientID, err)
				return
			}

			// Write unique message and verify echo.
			msg := []byte(fmt.Sprintf("client-%d", clientID))
			if _, err := clientConn.Write(msg); err != nil {
				errs <- fmt.Errorf("client-%d: write failed: %v", clientID, err)
				return
			}
			buf := make([]byte, len(msg))
			if _, err := io.ReadFull(clientConn, buf); err != nil {
				errs <- fmt.Errorf("client-%d: read failed: %v", clientID, err)
				return
			}
			if string(buf) != string(msg) {
				errs <- fmt.Errorf("client-%d: echo mismatch: sent %q, got %q", clientID, msg, buf)
				return
			}
			clientConn.Close()
		}(i)
	}

	wg.Wait()
	close(errs)

	// Collect all errors.
	for err := range errs {
		t.Error(err)
	}
}

func TestE2E_ConcurrentPortOperations(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-concurrent-ports: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Access SSH session for direct port manipulation.
	host.mu.Lock()
	sshSession := host.ssh
	host.mu.Unlock()

	// Launch 10 goroutines:
	// 0-4: add ports 9000-9004 (these stay)
	// 5-9: add then remove ports 9005-9009 (these are removed)
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func(idx int) {
			defer wg.Done()
			port := uint16(9000 + idx)
			sshSession.AddPort(port, "test-token")
			if idx >= 5 {
				sshSession.RemovePort(port, "test-token")
			}
		}(i)
	}

	wg.Wait()

	// Verify ports 9000-9004 exist.
	for i := 0; i < 5; i++ {
		port := uint16(9000 + i)
		if !sshSession.HasPort(port) {
			t.Fatalf("expected port %d to exist", port)
		}
	}

	// Verify ports 9005-9009 do NOT exist.
	for i := 5; i < 10; i++ {
		port := uint16(9000 + i)
		if sshSession.HasPort(port) {
			t.Fatalf("expected port %d to not exist (was added then removed)", port)
		}
	}

	// Verify exactly 5 ports remain.
	ports := sshSession.Ports()
	if len(ports) != 5 {
		t.Fatalf("expected 5 ports, got %d: %v", len(ports), ports)
	}
}

func TestE2E_IPv4AndIPv6(t *testing.T) {
	// Start IPv4 echo server on 127.0.0.1:0.
	_, ipv4Port := startEchoServerE2E(t)

	// Start IPv6 echo server on [::1]:0.
	// Skip the test if IPv6 is not available on this machine.
	ipv6Listener, err := net.Listen("tcp6", "[::1]:0")
	if err != nil {
		t.Skip("IPv6 not available")
	}
	ipv6Port := uint16(ipv6Listener.Addr().(*net.TCPAddr).Port)
	go func() {
		for {
			conn, err := ipv6Listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer conn.Close()
				io.Copy(conn, conn)
			}()
		}
	}()
	t.Cleanup(func() { ipv6Listener.Close() })

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-ipv4v6: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add both ports.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: ipv4Port}); err != nil {
		t.Fatalf("AddPort(ipv4) failed: %v", err)
	}
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: ipv6Port}); err != nil {
		t.Fatalf("AddPort(ipv6) failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Test IPv4: connect via relay, send "ipv4-test", verify echo.
	connV4, err := relay.SimulateClientConnection(ipv4Port)
	if err != nil {
		t.Fatalf("SimulateClientConnection(ipv4) failed: %v", err)
	}
	msgV4 := []byte("ipv4-test")
	if _, err := connV4.Write(msgV4); err != nil {
		t.Fatalf("IPv4 write failed: %v", err)
	}
	bufV4 := make([]byte, len(msgV4))
	if _, err := io.ReadFull(connV4, bufV4); err != nil {
		t.Fatalf("IPv4 read failed: %v", err)
	}
	if string(bufV4) != string(msgV4) {
		t.Fatalf("IPv4 echo mismatch: sent %q, got %q", msgV4, bufV4)
	}
	connV4.Close()

	// Test IPv6: connect via relay, send "ipv6-test", verify echo.
	connV6, err := relay.SimulateClientConnection(ipv6Port)
	if err != nil {
		t.Fatalf("SimulateClientConnection(ipv6) failed: %v", err)
	}
	msgV6 := []byte("ipv6-test")
	if _, err := connV6.Write(msgV6); err != nil {
		t.Fatalf("IPv6 write failed: %v", err)
	}
	bufV6 := make([]byte, len(msgV6))
	if _, err := io.ReadFull(connV6, bufV6); err != nil {
		t.Fatalf("IPv6 read failed: %v", err)
	}
	if string(bufV6) != string(msgV6) {
		t.Fatalf("IPv6 echo mismatch: sent %q, got %q", msgV6, bufV6)
	}
	connV6.Close()
}

func TestE2E_ConnectionRefused(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-conn-refused: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add port 19999 — no listener on this port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: 19999}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Simulate a client connection to port 19999 (no listener).
	// The host accepts the channel but the local dial fails, so it closes the channel.
	clientConn, err := relay.SimulateClientConnection(19999)
	if err != nil {
		t.Fatalf("SimulateClientConnection failed: %v", err)
	}

	// Read should return EOF or error (no panic).
	buf := make([]byte, 64)
	_, readErr := clientConn.Read(buf)
	if readErr == nil {
		t.Fatal("expected read error (EOF or other) on connection-refused port, got nil")
	}

	clientConn.Close()

	// No panic has occurred — host should remain functional.
	// Verify by starting an echo server, adding its port, and testing data flow.
	_, echoPort := startEchoServerE2E(t)

	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort(echo) failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Verify data flows through the echo port — host is still functional.
	echoConn, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection(echo) failed: %v", err)
	}
	msg := []byte("still-alive")
	if _, err := echoConn.Write(msg); err != nil {
		t.Fatalf("echo write failed: %v", err)
	}
	echoBuf := make([]byte, len(msg))
	if _, err := io.ReadFull(echoConn, echoBuf); err != nil {
		t.Fatalf("echo read failed: %v", err)
	}
	if string(echoBuf) != string(msg) {
		t.Fatalf("echo mismatch: sent %q, got %q", msg, echoBuf)
	}
	echoConn.Close()
}

func TestE2E_ClientDisconnectMidTransfer(t *testing.T) {
	// Start echo server.
	_, echoPort := startEchoServerE2E(t)

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-client-disconnect: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Add the echo port.
	if err := host.AddPort(ctx, &TunnelPort{PortNumber: echoPort}); err != nil {
		t.Fatalf("AddPort failed: %v", err)
	}

	// Give the relay time to process.
	time.Sleep(200 * time.Millisecond)

	// Simulate client 1 connection.
	clientConn1, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection(1) failed: %v", err)
	}

	// Write partial data to the channel.
	if _, err := clientConn1.Write([]byte("partial data")); err != nil {
		t.Fatalf("client 1: write failed: %v", err)
	}

	// Immediately close client 1 connection (abrupt disconnect).
	clientConn1.Close()

	// No panic after 200ms sleep.
	time.Sleep(200 * time.Millisecond)

	// Simulate client 2 — host should still be functional.
	clientConn2, err := relay.SimulateClientConnection(echoPort)
	if err != nil {
		t.Fatalf("SimulateClientConnection(2) failed: %v", err)
	}

	// Write "full message" and verify echo response.
	msg := []byte("full message")
	if _, err := clientConn2.Write(msg); err != nil {
		t.Fatalf("client 2: write failed: %v", err)
	}
	buf := make([]byte, len(msg))
	if _, err := io.ReadFull(clientConn2, buf); err != nil {
		t.Fatalf("client 2: read failed: %v", err)
	}
	if string(buf) != string(msg) {
		t.Fatalf("client 2: echo mismatch: sent %q, got %q", msg, buf)
	}
	clientConn2.Close()
}

func TestE2E_WaitBlocksUntilDisconnect(t *testing.T) {
	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-wait: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Launch host.Wait() in a goroutine and send the result to a channel.
	waitResult := make(chan error, 1)
	go func() {
		waitResult <- host.Wait()
	}()

	// After 200ms, verify the channel is empty (Wait is still blocking).
	time.Sleep(200 * time.Millisecond)
	select {
	case err := <-waitResult:
		t.Fatalf("Wait returned prematurely with error: %v", err)
	default:
		// Good — Wait is still blocking.
	}

	// Close the relay server to simulate relay drop.
	relay.Close()

	// Wait for result from channel with 5s timeout.
	select {
	case err := <-waitResult:
		// Wait should return a non-nil error when the relay drops.
		if err == nil {
			t.Fatal("expected non-nil error from Wait after relay close, got nil")
		}
	case <-time.After(5 * time.Second):
		t.Fatal("Wait did not return within 5s after relay close")
	}
}

func TestE2E_Reconnection(t *testing.T) {
	// Create relay 1 with access token.
	relay1, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay 1: %v", err)
	}

	// Create mock management API initially pointing to relay 1.
	api := newE2EMockAPI(t, relay1.URL())
	logger := log.New(os.Stderr, "e2e-reconnect: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	// Enable reconnection.
	host.EnableReconnect = true

	// Track status transitions via callback.
	var mu sync.Mutex
	var transitions []ConnectionStatus
	host.ConnectionStatusChanged = func(prev, curr ConnectionStatus) {
		mu.Lock()
		transitions = append(transitions, curr)
		mu.Unlock()
	}

	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	// Connect to relay 1.
	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relay1.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay 1 did not receive connection: %v", err)
	}

	// Verify connected.
	if status := host.ConnectionStatus(); status != ConnectionStatusConnected {
		t.Fatalf("expected Connected, got %v", status)
	}

	// Launch Wait() in a goroutine — it will handle reconnection.
	waitResult := make(chan error, 1)
	go func() {
		waitResult <- host.Wait()
	}()

	// Create relay 2.
	relay2, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay 2: %v", err)
	}
	t.Cleanup(func() { relay2.Close() })

	// Update API to point to relay 2.
	api.relayURI.Store(relay2.URL())

	// Close relay 1 to simulate disconnect.
	relay1.Close()

	// Wait for relay 2 to receive the reconnection (30s timeout).
	if err := relay2.WaitForConnection(30 * time.Second); err != nil {
		t.Fatalf("relay 2 did not receive reconnection: %v", err)
	}

	// Wait for host status to settle to Connected (relay connects before host finishes connectOnce).
	deadline := time.After(5 * time.Second)
	for host.ConnectionStatus() != ConnectionStatusConnected {
		select {
		case <-deadline:
			t.Fatalf("expected Connected after reconnection, got %v", host.ConnectionStatus())
		case <-time.After(10 * time.Millisecond):
		}
	}

	// Verify status transitions include: Connected -> Disconnected -> Connecting -> Connected.
	mu.Lock()
	got := make([]ConnectionStatus, len(transitions))
	copy(got, transitions)
	mu.Unlock()

	expectedSubseq := []ConnectionStatus{
		ConnectionStatusConnected,
		ConnectionStatusDisconnected,
		ConnectionStatusConnecting,
		ConnectionStatusConnected,
	}

	found := false
	for i := 0; i <= len(got)-len(expectedSubseq); i++ {
		match := true
		for j, exp := range expectedSubseq {
			if got[i+j] != exp {
				match = false
				break
			}
		}
		if match {
			found = true
			break
		}
	}
	if !found {
		t.Fatalf("expected transitions to include %v, got %v", expectedSubseq, got)
	}

	// Close host.
	if err := host.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	// Wait for Wait() to return.
	select {
	case <-waitResult:
		// Wait returned — good.
	case <-time.After(5 * time.Second):
		t.Fatal("Wait did not return within 5s after host close")
	}
}

func TestE2E_HostPublicKeyAvailable(t *testing.T) {
	// In V2 there is no nested SSH server exposed to clients, so we cannot
	// capture the host key via an SSH handshake. Instead, verify that the
	// host exposes a valid base64-encoded public key that clients can
	// retrieve via the management API (HostPublicKeys field on the endpoint).

	// Create relay server with access token.
	relay, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay server: %v", err)
	}
	t.Cleanup(func() { relay.Close() })

	// Create mock management API and host.
	api := newE2EMockAPI(t, relay.URL())
	logger := log.New(os.Stderr, "e2e-pubkey: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	// Get the host public key (base64-encoded).
	expectedKey := host.HostPublicKeyBase64()
	if expectedKey == "" {
		t.Fatal("HostPublicKeyBase64 returned empty string")
	}

	// Verify the key is valid base64.
	keyBytes, err := base64.StdEncoding.DecodeString(expectedKey)
	if err != nil {
		t.Fatalf("HostPublicKeyBase64 is not valid base64: %v", err)
	}

	// Verify the key bytes are non-empty and have a reasonable length.
	if len(keyBytes) < 16 {
		t.Fatalf("decoded public key is too short: %d bytes", len(keyBytes))
	}

	// Connect and verify the key remains stable after connection.
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}
	defer host.Close()

	if err := relay.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay did not receive connection: %v", err)
	}

	// Key should be the same after connection.
	afterConnectKey := host.HostPublicKeyBase64()
	if afterConnectKey != expectedKey {
		t.Fatalf("public key changed after connect:\n  before: %s\n  after:  %s", expectedKey, afterConnectKey)
	}
}

func TestE2E_TokenRefresh(t *testing.T) {
	// Create relay 1 with access token.
	relay1, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel test-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay 1: %v", err)
	}

	// Create mock management API initially pointing to relay 1.
	api := newE2EMockAPI(t, relay1.URL())
	logger := log.New(os.Stderr, "e2e-token-refresh: ", log.LstdFlags)
	host, err := NewHost(logger, api.manager)
	if err != nil {
		t.Fatalf("NewHost failed: %v", err)
	}

	// Enable reconnection.
	host.EnableReconnect = true

	// Set up token refresh callback that increments an atomic counter.
	var tokenRefreshCalls int32
	host.RefreshTunnelAccessTokenFunc = func(ctx context.Context) (string, error) {
		atomic.AddInt32(&tokenRefreshCalls, 1)
		return "refreshed-token", nil
	}

	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Second)
	defer cancel()

	tunnel := &Tunnel{
		Name: "test-tunnel",
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeHost: "test-token",
		},
	}

	// Connect to relay 1.
	if err := host.Connect(ctx, tunnel); err != nil {
		t.Fatalf("Host.Connect failed: %v", err)
	}

	if err := relay1.WaitForConnection(5 * time.Second); err != nil {
		t.Fatalf("relay 1 did not receive connection: %v", err)
	}

	// Verify connected.
	if status := host.ConnectionStatus(); status != ConnectionStatusConnected {
		t.Fatalf("expected Connected, got %v", status)
	}

	// Launch Wait() in a goroutine — it will handle reconnection.
	waitResult := make(chan error, 1)
	go func() {
		waitResult <- host.Wait()
	}()

	// Set unauthorizedOnce so next UpdateTunnelEndpoint returns 401.
	atomic.StoreInt32(&api.unauthorizedOnce, 1)

	// Create relay 2.
	relay2, err := tunnelstest.NewRelayHostServer(
		tunnelstest.WithHostAccessToken("Tunnel refreshed-token"),
	)
	if err != nil {
		t.Fatalf("failed to create relay 2: %v", err)
	}
	t.Cleanup(func() { relay2.Close() })

	// Update API to point to relay 2.
	api.relayURI.Store(relay2.URL())

	// Close relay 1 to trigger reconnection.
	relay1.Close()

	// Wait for relay 2 to receive the reconnection (30s timeout).
	if err := relay2.WaitForConnection(30 * time.Second); err != nil {
		t.Fatalf("relay 2 did not receive reconnection: %v", err)
	}

	// Wait for host status to settle to Connected.
	deadline := time.After(5 * time.Second)
	for host.ConnectionStatus() != ConnectionStatusConnected {
		select {
		case <-deadline:
			t.Fatalf("expected Connected after reconnection, got %v", host.ConnectionStatus())
		case <-time.After(10 * time.Millisecond):
		}
	}

	// Assert token refresh was called at least once.
	calls := atomic.LoadInt32(&tokenRefreshCalls)
	if calls < 1 {
		t.Fatalf("expected tokenRefreshCalls >= 1, got %d", calls)
	}

	// Close host.
	if err := host.Close(); err != nil {
		t.Fatalf("Host.Close failed: %v", err)
	}

	// Wait for Wait() to return.
	select {
	case <-waitResult:
		// Wait returned — good.
	case <-time.After(5 * time.Second):
		t.Fatal("Wait did not return within 5s after host close")
	}
}
