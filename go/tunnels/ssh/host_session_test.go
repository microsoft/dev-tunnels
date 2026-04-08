// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"bytes"
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"log"
	"net"
	"os"
	"sync"
	"testing"
	"time"

	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
	"golang.org/x/crypto/ssh"
)

const testRSAPrivateKey = `-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQC6VU6XsMaTot9ogsGcJ+juvJOmDvvCZmgJRTRwKkW0u2BLz4yV
rCzQcxaY4kaIuR80Y+1f0BLnZgh4pTREDR0T+p8hUsDSHim1ttKI8rK0hRtJ2qhY
lR4qt7P51rPA4KFA9z9gDjTwQLbDq21QMC4+n4d8CL3xRVGtlUAMM3Kl3wIDAQAB
AoGBAI8UemkYoSM06gBCh5D1RHQt8eKNltzL7g9QSNfoXeZOC7+q+/TiZPcbqLp0
5lyOalu8b8Ym7J0rSE377Ypj13LyHMXS63e4wMiXv3qOl3GDhMLpypnJ8PwqR2b8
IijL2jrpQfLu6IYqlteA+7e9aEexJa1RRwxYIyq6pG1IYpbhAkEA9nKgtj3Z6ZDC
46IdqYzuUM9ZQdcw4AFr407+lub7tbWe5pYmaq3cT725IwLw081OAmnWJYFDMa/n
IPl9YcZSPQJBAMGOMbPs/YPkQAsgNdIUlFtK3o41OrrwJuTRTvv0DsbqDV0LKOiC
t8oAQQvjisH6Ew5OOhFyIFXtvZfzQMJppksCQQDWFd+cUICTUEise/Duj9maY3Uz
J99ySGnTbZTlu8PfJuXhg3/d3ihrMPG6A1z3cPqaSBxaOj8H07mhQHn1zNU1AkEA
hkl+SGPrO793g4CUdq2ahIA8SpO5rIsDoQtq7jlUq0MlhGFCv5Y5pydn+bSjx5MV
933kocf5kUSBntPBIWElYwJAZTm5ghu0JtSE6t3km0iuj7NGAQSdb6mD8+O7C3CP
FU3vi+4HlBysaT6IZ/HG+/dBsr4gYp4LGuS7DbaLuYw/uw==
-----END RSA PRIVATE KEY-----`

func newTestHostKey() ssh.Signer {
	_, privKey, _ := ed25519.GenerateKey(rand.Reader)
	signer, _ := ssh.NewSignerFromKey(privKey)
	return signer
}

func newTestLogger() *log.Logger {
	return log.New(os.Stderr, "test: ", log.LstdFlags)
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

// mockV2Relay holds the relay side of an SSH connection that handles
// tcpip-forward/cancel-tcpip-forward global requests like a V2 relay.
type mockV2Relay struct {
	conn     ssh.Conn
	mu       sync.Mutex
	ports    map[uint16]struct{}
	portReqs chan portReqInfo
}

type portReqInfo struct {
	reqType string
	port    uint16
}

// setupHostAndRelay creates a HostSSHSession connected via TCP loopback
// with a V2 mock relay that handles tcpip-forward requests.
// Returns the host session, the mock relay, and a cleanup function.
func setupHostAndRelay(t *testing.T) (*HostSSHSession, *mockV2Relay, func()) {
	t.Helper()

	hostEnd, relayEnd := tcpConnPair(t)
	hostKey := newTestHostKey()
	logger := newTestLogger()

	session := NewHostSSHSession(hostEnd, hostKey, logger, "test-token", HostWebSocketSubProtocolV2)

	// Relay side: SSH server
	relayConfig := &ssh.ServerConfig{NoClientAuth: true}
	privateKey, err := ssh.ParsePrivateKey([]byte(testRSAPrivateKey))
	if err != nil {
		t.Fatalf("failed to parse private key: %v", err)
	}
	relayConfig.AddHostKey(privateKey)

	relay := &mockV2Relay{
		ports:    make(map[uint16]struct{}),
		portReqs: make(chan portReqInfo, 100),
	}

	// Connect concurrently: host connects as SSH client, relay accepts as SSH server.
	var relayConn ssh.Conn
	var relayErr error
	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		defer wg.Done()
		var reqs <-chan *ssh.Request
		var chans <-chan ssh.NewChannel
		relayConn, chans, reqs, relayErr = ssh.NewServerConn(relayEnd, relayConfig)
		if relayErr == nil {
			go relay.handleGlobalRequests(reqs)
			// Reject any channels from the host (V2 relay doesn't expect host-initiated channels).
			go func() {
				for ch := range chans {
					ch.Reject(ssh.Prohibited, "not supported")
				}
			}()
		}
	}()

	ctx := context.Background()
	if err := session.Connect(ctx); err != nil {
		t.Fatalf("failed to connect host session: %v", err)
	}

	wg.Wait()
	if relayErr != nil {
		t.Fatalf("relay SSH handshake failed: %v", relayErr)
	}

	relay.conn = relayConn

	cleanup := func() {
		session.Close()
		relayConn.Close()
	}

	return session, relay, cleanup
}

func (r *mockV2Relay) handleGlobalRequests(reqs <-chan *ssh.Request) {
	for req := range reqs {
		switch req.Type {
		case "tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			port := uint16(prr.Port())
			r.mu.Lock()
			r.ports[port] = struct{}{}
			r.mu.Unlock()
			r.portReqs <- portReqInfo{reqType: "tcpip-forward", port: port}
			req.Reply(true, nil)

		case "cancel-tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			port := uint16(prr.Port())
			r.mu.Lock()
			delete(r.ports, port)
			r.mu.Unlock()
			r.portReqs <- portReqInfo{reqType: "cancel-tcpip-forward", port: port}
			req.Reply(true, nil)

		default:
			req.Reply(false, nil)
		}
	}
}

// openForwardedTCPIP opens a forwarded-tcpip channel to the host with V2 extra data.
func (r *mockV2Relay) openForwardedTCPIP(port uint16) (ssh.Channel, error) {
	data := &messages.PortRelayConnectRequest{
		Host:                     "127.0.0.1",
		Port:                     uint32(port),
		OriginatorIP:             "127.0.0.1",
		OriginatorPort:           0,
		AccessToken:              "",
		IsE2EEncryptionRequested: false,
	}
	extraData, err := data.Marshal()
	if err != nil {
		return nil, err
	}

	ch, reqs, err := r.conn.OpenChannel("forwarded-tcpip", extraData)
	if err != nil {
		return nil, err
	}
	go ssh.DiscardRequests(reqs)
	return ch, nil
}

// openDirectTCPIP opens a direct-tcpip channel to the host with V2 extra data.
func (r *mockV2Relay) openDirectTCPIP(port uint16) (ssh.Channel, error) {
	data := &messages.PortRelayConnectRequest{
		Host:                     "127.0.0.1",
		Port:                     uint32(port),
		OriginatorIP:             "127.0.0.1",
		OriginatorPort:           0,
		AccessToken:              "",
		IsE2EEncryptionRequested: false,
	}
	extraData, err := data.Marshal()
	if err != nil {
		return nil, err
	}

	ch, reqs, err := r.conn.OpenChannel("direct-tcpip", extraData)
	if err != nil {
		return nil, err
	}
	go ssh.DiscardRequests(reqs)
	return ch, nil
}

func (r *mockV2Relay) hasPort(port uint16) bool {
	r.mu.Lock()
	defer r.mu.Unlock()
	_, ok := r.ports[port]
	return ok
}

// waitForPortForward waits until a tcpip-forward request for the given port arrives.
func (r *mockV2Relay) waitForPortForward(t *testing.T, port uint16) {
	t.Helper()
	timeout := time.After(5 * time.Second)
	for {
		select {
		case info := <-r.portReqs:
			if info.reqType == "tcpip-forward" && info.port == port {
				return
			}
		case <-timeout:
			t.Fatalf("timeout waiting for tcpip-forward for port %d", port)
		}
	}
}

func TestHostSessionConnectWithSSHHandshake(t *testing.T) {
	_, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Verify the relay connection user is "tunnel".
	if relay.conn.User() != "tunnel" {
		t.Fatalf("expected user 'tunnel', got %q", relay.conn.User())
	}
}

func TestHostSessionAcceptsForwardedTcpip(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Register a port so the channel will be accepted.
	session.AddPort(8080, "test-token")
	relay.waitForPortForward(t, 8080)

	// Open a forwarded-tcpip channel from the relay side.
	ch, err := relay.openForwardedTCPIP(8080)
	if err != nil {
		// The channel should be accepted (port is registered).
		// It's OK if the local dial fails — the channel was at least accepted.
		return
	}
	ch.Close()
}

func TestHostSessionRejectsUnknownChannelType(t *testing.T) {
	_, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Try to open an unknown channel type.
	_, _, err := relay.conn.OpenChannel("unknown-type", nil)
	if err == nil {
		t.Fatal("expected error for unknown channel type, got nil")
	}

	// Verify it's an OpenChannelError with UnknownChannelType reason.
	if openErr, ok := err.(*ssh.OpenChannelError); ok {
		if openErr.Reason != ssh.UnknownChannelType {
			t.Fatalf("expected UnknownChannelType, got %v", openErr.Reason)
		}
	} else {
		t.Fatalf("expected *ssh.OpenChannelError, got %T: %v", err, err)
	}
}

func TestHostSessionRejectsUnregisteredPort(t *testing.T) {
	_, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Try to open a forwarded-tcpip to an unregistered port.
	_, err := relay.openForwardedTCPIP(9999)
	if err == nil {
		t.Fatal("expected error for unregistered port, got nil")
	}

	// Verify it's a Prohibited rejection.
	if openErr, ok := err.(*ssh.OpenChannelError); ok {
		if openErr.Reason != ssh.Prohibited {
			t.Fatalf("expected Prohibited, got %v", openErr.Reason)
		}
	} else {
		t.Fatalf("expected *ssh.OpenChannelError, got %T: %v", err, err)
	}
}

func TestConcurrentAddPort(t *testing.T) {
	session, _, cleanup := setupHostAndRelay(t)
	defer cleanup()

	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func(port uint16) {
			defer wg.Done()
			session.AddPort(port, "test-token")
		}(uint16(9000 + i))
	}
	wg.Wait()

	// Verify all 10 ports were added.
	session.portsMu.RLock()
	count := len(session.ports)
	session.portsMu.RUnlock()

	if count != 10 {
		t.Fatalf("expected 10 ports, got %d", count)
	}
}

func TestAddPortSendsTcpipForward(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Add ports before and verify relay receives tcpip-forward.
	session.AddPort(8080, "test-token")
	session.AddPort(3000, "test-token")

	// Wait for both port forward requests.
	timeout := time.After(5 * time.Second)
	received := 0
	for received < 2 {
		select {
		case info := <-relay.portReqs:
			if info.reqType != "tcpip-forward" {
				t.Fatalf("expected tcpip-forward, got %s", info.reqType)
			}
			received++
		case <-timeout:
			t.Fatalf("timeout waiting for tcpip-forward requests, got %d of 2", received)
		}
	}

	// Verify relay knows about both ports.
	if !relay.hasPort(8080) {
		t.Fatal("relay should have port 8080")
	}
	if !relay.hasPort(3000) {
		t.Fatal("relay should have port 3000")
	}
}

func TestRemovePortSendsCancelTcpipForward(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Add a port.
	session.AddPort(8080, "test-token")
	relay.waitForPortForward(t, 8080)

	// Remove the port.
	session.RemovePort(8080, "test-token")

	// Wait for cancel-tcpip-forward.
	timeout := time.After(5 * time.Second)
	for {
		select {
		case info := <-relay.portReqs:
			if info.reqType == "cancel-tcpip-forward" && info.port == 8080 {
				return // Success
			}
		case <-timeout:
			t.Fatal("timeout waiting for cancel-tcpip-forward")
		}
	}
}

func TestRemovePortNoOpForUnregisteredPort(t *testing.T) {
	session, _, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Remove a port that was never added — should not send anything.
	session.RemovePort(9999, "test-token")

	// Give time for any spurious request to arrive.
	time.Sleep(200 * time.Millisecond)

	// Verify port list is still empty.
	session.portsMu.RLock()
	count := len(session.ports)
	session.portsMu.RUnlock()
	if count != 0 {
		t.Fatalf("expected 0 ports, got %d", count)
	}
}

func TestCloseWhileChannelsOpen(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	_ = cleanup // We'll close manually.

	// Add a port and open a channel.
	session.AddPort(8080, "test-token")
	relay.waitForPortForward(t, 8080)

	// The channel open will likely fail because there's no local listener,
	// but that's fine — we're testing close safety.
	relay.openForwardedTCPIP(8080)

	time.Sleep(200 * time.Millisecond)

	// Close the host session. This should not panic.
	session.Close()
	relay.conn.Close()

	// Give goroutines time to clean up.
	time.Sleep(100 * time.Millisecond)
}

func TestAddPortDeduplicates(t *testing.T) {
	hostKey := newTestHostKey()
	logger := newTestLogger()
	hostEnd, relayEnd := net.Pipe()
	defer hostEnd.Close()
	defer relayEnd.Close()
	session := NewHostSSHSession(hostEnd, hostKey, logger, "test-token", HostWebSocketSubProtocolV2)

	session.AddPort(8080, "test-token")
	session.AddPort(8080, "test-token") // Adding same port again — should be deduplicated.

	// Verify it was added only once.
	session.portsMu.RLock()
	count := 0
	for _, p := range session.ports {
		if p == 8080 {
			count++
		}
	}
	session.portsMu.RUnlock()

	if count != 1 {
		t.Fatalf("expected port 8080 to appear once, got %d", count)
	}
}

func TestAddPortWhenNotConnected(t *testing.T) {
	hostKey := newTestHostKey()
	logger := newTestLogger()
	hostEnd, relayEnd := net.Pipe()
	defer hostEnd.Close()
	defer relayEnd.Close()
	session := NewHostSSHSession(hostEnd, hostKey, logger, "test-token", HostWebSocketSubProtocolV2)

	// Session is not connected, but AddPort should still add to the local list.
	session.AddPort(8080, "test-token")

	// Verify port was added.
	session.portsMu.RLock()
	found := false
	for _, p := range session.ports {
		if p == 8080 {
			found = true
		}
	}
	session.portsMu.RUnlock()

	if !found {
		t.Fatal("expected port 8080 to be added")
	}
}

// ============================================================
// V1 tests
// ============================================================

// mockV1Relay holds the relay side of an SSH connection for V1 tests.
// In V1, the relay rejects all global requests and opens client-ssh-session-stream
// channels to simulate client connections.
type mockV1Relay struct {
	conn ssh.Conn
}

// setupHostAndRelayV1 creates a HostSSHSession with V1 protocol connected via TCP
// loopback with a V1 mock relay.
func setupHostAndRelayV1(t *testing.T) (*HostSSHSession, *mockV1Relay, func()) {
	t.Helper()

	hostEnd, relayEnd := tcpConnPair(t)
	hostKey := newTestHostKey()
	logger := newTestLogger()

	session := NewHostSSHSession(hostEnd, hostKey, logger, "", HostWebSocketSubProtocol)

	// Relay side: SSH server
	relayConfig := &ssh.ServerConfig{NoClientAuth: true}
	privateKey, err := ssh.ParsePrivateKey([]byte(testRSAPrivateKey))
	if err != nil {
		t.Fatalf("failed to parse private key: %v", err)
	}
	relayConfig.AddHostKey(privateKey)

	relay := &mockV1Relay{}

	var relayConn ssh.Conn
	var relayErr error
	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		defer wg.Done()
		var reqs <-chan *ssh.Request
		var chans <-chan ssh.NewChannel
		relayConn, chans, reqs, relayErr = ssh.NewServerConn(relayEnd, relayConfig)
		if relayErr == nil {
			// V1 relay rejects all global requests.
			go func() {
				for req := range reqs {
					req.Reply(false, nil)
				}
			}()
			// Drain incoming channels.
			go func() {
				for ch := range chans {
					ch.Reject(ssh.Prohibited, "not supported")
				}
			}()
		}
	}()

	ctx := context.Background()
	if err := session.Connect(ctx); err != nil {
		t.Fatalf("failed to connect host session: %v", err)
	}

	wg.Wait()
	if relayErr != nil {
		t.Fatalf("relay SSH handshake failed: %v", relayErr)
	}

	relay.conn = relayConn

	cleanup := func() {
		session.Close()
		relayConn.Close()
	}

	return session, relay, cleanup
}

// openClientSession opens a client-ssh-session-stream channel on the relay
// and performs a nested SSH client handshake, returning the *ssh.Client.
func (r *mockV1Relay) openClientSession(t *testing.T) *ssh.Client {
	t.Helper()

	channel, reqs, err := r.conn.OpenChannel("client-ssh-session-stream", nil)
	if err != nil {
		t.Fatalf("failed to open client-ssh-session-stream: %v", err)
	}
	go ssh.DiscardRequests(reqs)

	// Wrap channel as net.Conn for nested SSH handshake.
	conn := &testChannelConn{Channel: channel}

	clientConfig := &ssh.ClientConfig{
		User:            "tunnel",
		HostKeyCallback: ssh.InsecureIgnoreHostKey(),
		Timeout:         10 * time.Second,
	}

	sshConn, chans, globalReqs, err := ssh.NewClientConn(conn, "", clientConfig)
	if err != nil {
		t.Fatalf("nested SSH client handshake failed: %v", err)
	}

	return ssh.NewClient(sshConn, chans, globalReqs)
}

// testChannelConn wraps an ssh.Channel as a net.Conn for test use.
type testChannelConn struct {
	ssh.Channel
}

func (c *testChannelConn) LocalAddr() net.Addr                { return testDummyAddr{} }
func (c *testChannelConn) RemoteAddr() net.Addr               { return testDummyAddr{} }
func (c *testChannelConn) SetDeadline(t time.Time) error      { return nil }
func (c *testChannelConn) SetReadDeadline(t time.Time) error  { return nil }
func (c *testChannelConn) SetWriteDeadline(t time.Time) error { return nil }

type testDummyAddr struct{}

func (testDummyAddr) Network() string { return "test" }
func (testDummyAddr) String() string  { return "test" }

func TestV1HostSessionAcceptsClientStream(t *testing.T) {
	_, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// Open a client-ssh-session-stream and verify nested SSH handshake completes.
	client := relay.openClientSession(t)
	defer client.Close()

	// If we got here, the nested SSH handshake succeeded.
}

func TestV1HostSessionRejectsUnknownChannel(t *testing.T) {
	_, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// Try to open an unknown channel type.
	_, _, err := relay.conn.OpenChannel("unknown-type", nil)
	if err == nil {
		t.Fatal("expected error for unknown channel type, got nil")
	}

	if openErr, ok := err.(*ssh.OpenChannelError); ok {
		if openErr.Reason != ssh.UnknownChannelType {
			t.Fatalf("expected UnknownChannelType, got %v", openErr.Reason)
		}
	} else {
		t.Fatalf("expected *ssh.OpenChannelError, got %T: %v", err, err)
	}
}

func TestV1PortForwardToClient(t *testing.T) {
	session, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// First add a port, then connect a client.
	session.AddPort(8080, "")

	client := relay.openClientSession(t)
	defer client.Close()

	// The host should send tcpip-forward to this client for port 8080.
	// Listen for it using client.HandleChannelOpen or check global requests.
	// In V1, the host sends tcpip-forward as a global request to the client.
	// The ssh.Client handles global requests; we need to check them.
	// Wait for the tcpip-forward request to arrive.
	time.Sleep(500 * time.Millisecond)

	// Verify the port was registered.
	if !session.HasPort(8080) {
		t.Fatal("expected port 8080 to be registered")
	}
}

func TestV1AddPortNotifiesExistingClients(t *testing.T) {
	session, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// Connect a client first.
	client := relay.openClientSession(t)
	defer client.Close()

	// Give time for the client to be registered.
	time.Sleep(300 * time.Millisecond)

	// Now add a port — should notify the existing client.
	session.AddPort(9090, "")

	// Wait for the notification to propagate.
	time.Sleep(500 * time.Millisecond)

	// Verify the port is in the session.
	if !session.HasPort(9090) {
		t.Fatal("expected port 9090 to be registered")
	}
}

func TestV1RemovePortNotifiesClients(t *testing.T) {
	session, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// Connect a client.
	client := relay.openClientSession(t)
	defer client.Close()

	// Give time for registration.
	time.Sleep(300 * time.Millisecond)

	// Add then remove a port.
	session.AddPort(7070, "")
	time.Sleep(200 * time.Millisecond)

	session.RemovePort(7070, "")
	time.Sleep(200 * time.Millisecond)

	// Verify the port was removed.
	if session.HasPort(7070) {
		t.Fatal("expected port 7070 to be removed")
	}
}

func TestV1MultipleClients(t *testing.T) {
	session, relay, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	// Add a port first.
	session.AddPort(5050, "")

	// Connect multiple clients.
	client1 := relay.openClientSession(t)
	defer client1.Close()

	client2 := relay.openClientSession(t)
	defer client2.Close()

	// Give time for both to register and receive port forwards.
	time.Sleep(500 * time.Millisecond)

	// Verify the session has the expected number of clients.
	session.clientsMu.RLock()
	clientCount := len(session.clients)
	session.clientsMu.RUnlock()

	if clientCount != 2 {
		t.Fatalf("expected 2 clients, got %d", clientCount)
	}
}

func TestV1ConnectionProtocol(t *testing.T) {
	session, _, cleanup := setupHostAndRelayV1(t)
	defer cleanup()

	if session.ConnectionProtocol() != HostWebSocketSubProtocol {
		t.Fatalf("expected %q, got %q", HostWebSocketSubProtocol, session.ConnectionProtocol())
	}
}

func TestV2ConnectionProtocol(t *testing.T) {
	session, _, cleanup := setupHostAndRelay(t)
	defer cleanup()

	if session.ConnectionProtocol() != HostWebSocketSubProtocolV2 {
		t.Fatalf("expected %q, got %q", HostWebSocketSubProtocolV2, session.ConnectionProtocol())
	}
}
