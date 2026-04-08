// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"bytes"
	"crypto/sha256"
	"errors"
	"fmt"
	"io"
	"net"
	"sync"
	"testing"
	"time"

	"golang.org/x/crypto/ssh"
)

// startEchoServer starts a TCP echo server on a random port and returns
// the listener and port number. The server echoes all received data back.
func startEchoServer(t *testing.T, network string, addr string) (net.Listener, uint16) {
	t.Helper()
	listener, err := net.Listen(network, addr)
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

	port := uint16(listener.Addr().(*net.TCPAddr).Port)
	return listener, port
}

func TestPortForwardingEchoServer(t *testing.T) {
	// Start echo server.
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	// Setup host session with the port registered.
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	// Open a forwarded-tcpip channel from the relay.
	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open forwarded-tcpip channel: %v", err)
	}
	defer ch.Close()

	// Send data through the tunnel.
	testData := []byte("hello tunnel")
	_, err = ch.Write(testData)
	if err != nil {
		t.Fatalf("failed to write: %v", err)
	}

	// Read the echo response.
	buf := make([]byte, len(testData))
	_, err = io.ReadFull(ch, buf)
	if err != nil {
		t.Fatalf("failed to read echo: %v", err)
	}

	if string(buf) != string(testData) {
		t.Fatalf("expected %q, got %q", testData, buf)
	}
}

func TestPortForwardingIPv4(t *testing.T) {
	// Start echo server on IPv4.
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open channel: %v", err)
	}
	defer ch.Close()

	_, err = ch.Write([]byte("ipv4 test"))
	if err != nil {
		t.Fatalf("failed to write: %v", err)
	}

	buf := make([]byte, 9)
	_, err = io.ReadFull(ch, buf)
	if err != nil {
		t.Fatalf("failed to read: %v", err)
	}
	if string(buf) != "ipv4 test" {
		t.Fatalf("expected 'ipv4 test', got %q", buf)
	}
}

func TestPortForwardingConnectionRefused(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Add a port that has no listener (connection will be refused).
	session.AddPort(19999, "test-token")
	relay.waitForPortForward(t, 19999)

	ch, err := relay.openForwardedTCPIP(19999)
	if err != nil {
		// Channel may be rejected, which is acceptable.
		return
	}

	// The channel should be closed by the host after connection refused.
	buf := make([]byte, 1)
	_, err = ch.Read(buf)
	if err == nil {
		t.Fatal("expected error reading from channel with no backend")
	}
}

func TestPortForwardingBidirectional(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open channel: %v", err)
	}
	defer ch.Close()

	// Send multiple messages and verify echo.
	for i := 0; i < 5; i++ {
		msg := fmt.Sprintf("message %d", i)
		_, err = ch.Write([]byte(msg))
		if err != nil {
			t.Fatalf("write %d failed: %v", i, err)
		}

		buf := make([]byte, len(msg))
		_, err = io.ReadFull(ch, buf)
		if err != nil {
			t.Fatalf("read %d failed: %v", i, err)
		}
		if string(buf) != msg {
			t.Fatalf("message %d: expected %q, got %q", i, msg, buf)
		}
	}
}

func TestDirectTcpipRejectsUnregisteredPort(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	// Register only echoPort — the unregistered port should be rejected.
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	// Try to open a direct-tcpip channel to an unregistered port.
	unregisteredPort := echoPort + 1000
	_, err := relay.openDirectTCPIP(unregisteredPort)
	if err == nil {
		t.Fatal("expected direct-tcpip to unregistered port to be rejected, got nil error")
	}

	// Verify it's a rejection with Prohibited reason.
	if openErr, ok := err.(*ssh.OpenChannelError); ok {
		if openErr.Reason != ssh.Prohibited {
			t.Fatalf("expected Prohibited rejection reason, got %v", openErr.Reason)
		}
	} else {
		t.Fatalf("expected *ssh.OpenChannelError, got %T: %v", err, err)
	}
}

func TestDirectTcpipChannel(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openDirectTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open direct-tcpip: %v", err)
	}
	defer ch.Close()

	msg := []byte("direct-tcpip-test")
	if _, err := ch.Write(msg); err != nil {
		t.Fatalf("failed to write: %v", err)
	}
	buf := make([]byte, len(msg))
	if _, err := io.ReadFull(ch, buf); err != nil {
		t.Fatalf("failed to read: %v", err)
	}
	if string(buf) != string(msg) {
		t.Fatalf("expected %q, got %q", msg, buf)
	}
}

func TestPortForwardingIPv6Fallback(t *testing.T) {
	// Start echo server on IPv6 only.
	echoListener, err := net.Listen("tcp6", "[::1]:0")
	if err != nil {
		t.Skip("IPv6 not available")
	}
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
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open channel: %v", err)
	}
	defer ch.Close()

	testData := []byte("ipv6 test")
	_, err = ch.Write(testData)
	if err != nil {
		t.Fatalf("failed to write: %v", err)
	}

	buf := make([]byte, len(testData))
	_, err = io.ReadFull(ch, buf)
	if err != nil {
		t.Fatalf("failed to read: %v", err)
	}
	if string(buf) != string(testData) {
		t.Fatalf("expected %q, got %q", testData, buf)
	}
}

func TestPortForwardingLargePayload(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open channel: %v", err)
	}

	// Send 1MB of data.
	payload := make([]byte, 1024*1024)
	for i := range payload {
		payload[i] = byte(i % 256)
	}
	expectedHash := sha256.Sum256(payload)

	var wg sync.WaitGroup
	var writeErr error

	wg.Add(1)
	go func() {
		defer wg.Done()
		_, writeErr = ch.Write(payload)
		ch.CloseWrite()
	}()

	// Read all echoed data.
	received := new(bytes.Buffer)
	_, err = io.Copy(received, ch)
	if err != nil && !errors.Is(err, io.EOF) {
		t.Fatalf("failed to read echo: %v", err)
	}

	wg.Wait()
	if writeErr != nil {
		t.Fatalf("write error: %v", writeErr)
	}

	actualHash := sha256.Sum256(received.Bytes())
	if actualHash != expectedHash {
		t.Fatalf("SHA256 mismatch: payload integrity check failed (sent %d bytes, received %d bytes)",
			len(payload), received.Len())
	}
}

func TestPortForwardingConcurrentConnections(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	var wg sync.WaitGroup
	errc := make(chan error, 5)

	for i := 0; i < 5; i++ {
		wg.Add(1)
		go func(idx int) {
			defer wg.Done()

			ch, err := relay.openForwardedTCPIP(echoPort)
			if err != nil {
				errc <- fmt.Errorf("connection %d: open channel failed: %w", idx, err)
				return
			}
			defer ch.Close()

			msg := fmt.Sprintf("concurrent-%d", idx)
			_, err = ch.Write([]byte(msg))
			if err != nil {
				errc <- fmt.Errorf("connection %d: write failed: %w", idx, err)
				return
			}

			buf := make([]byte, len(msg))
			_, err = io.ReadFull(ch, buf)
			if err != nil {
				errc <- fmt.Errorf("connection %d: read failed: %w", idx, err)
				return
			}
			if string(buf) != msg {
				errc <- fmt.Errorf("connection %d: expected %q, got %q", idx, msg, buf)
			}
		}(i)
	}

	wg.Wait()
	close(errc)
	for err := range errc {
		t.Fatal(err)
	}
}

func TestAddPortNotifiesRelay(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Start an echo server.
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	// Add a port — relay should receive tcpip-forward.
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	// Verify relay knows about the port.
	if !relay.hasPort(echoPort) {
		t.Fatal("relay should have the port registered")
	}
}

func TestRemovePortNotifiesRelay(t *testing.T) {
	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()

	// Add a port first.
	session.AddPort(9090, "test-token")
	relay.waitForPortForward(t, 9090)

	// Now remove the port — relay should receive cancel-tcpip-forward.
	session.RemovePort(9090, "test-token")

	// Wait for cancel request.
	for {
		select {
		case info := <-relay.portReqs:
			if info.reqType == "cancel-tcpip-forward" && info.port == 9090 {
				// Verify the port was removed from the session's port list.
				session.portsMu.RLock()
				for _, p := range session.ports {
					if p == 9090 {
						session.portsMu.RUnlock()
						t.Fatal("port 9090 should have been removed")
					}
				}
				session.portsMu.RUnlock()
				return
			}
		case <-time.After(5 * time.Second):
			t.Fatal("timeout waiting for cancel-tcpip-forward notification")
			return
		}
	}
}

func TestForwardedTcpipChannel(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open forwarded-tcpip: %v", err)
	}
	defer ch.Close()

	msg := []byte("forwarded-tcpip-test")
	if _, err := ch.Write(msg); err != nil {
		t.Fatalf("failed to write: %v", err)
	}
	buf := make([]byte, len(msg))
	if _, err := io.ReadFull(ch, buf); err != nil {
		t.Fatalf("failed to read: %v", err)
	}
	if string(buf) != string(msg) {
		t.Fatalf("expected %q, got %q", msg, buf)
	}
}

func TestClientDisconnectMidTransfer(t *testing.T) {
	echoListener, echoPort := startEchoServer(t, "tcp4", "127.0.0.1:0")
	defer echoListener.Close()

	session, relay, cleanup := setupHostAndRelay(t)
	defer cleanup()
	session.AddPort(echoPort, "test-token")
	relay.waitForPortForward(t, echoPort)

	// Open a forwarded-tcpip channel.
	ch, err := relay.openForwardedTCPIP(echoPort)
	if err != nil {
		t.Fatalf("failed to open channel: %v", err)
	}

	// Write some data.
	ch.Write([]byte("partial data"))

	// Abruptly close the channel mid-transfer.
	ch.Close()

	// Wait a bit to ensure no panic or goroutine leak crashes.
	time.Sleep(200 * time.Millisecond)
}
