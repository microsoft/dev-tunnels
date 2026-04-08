// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"io"
	"sync"
	"testing"
	"time"

	"golang.org/x/crypto/ssh"
)

// mockSSHChannel is a mock ssh.Channel for testing channelConn.
type mockSSHChannel struct {
	readBuf  []byte
	writeBuf []byte
	closed   bool
	mu       sync.Mutex
	readCh   chan []byte
	closeCh  chan struct{}
}

func newMockChannel() *mockSSHChannel {
	return &mockSSHChannel{
		readCh:  make(chan []byte, 10),
		closeCh: make(chan struct{}),
	}
}

func (m *mockSSHChannel) Read(data []byte) (int, error) {
	m.mu.Lock()
	if m.closed {
		m.mu.Unlock()
		return 0, io.EOF
	}
	m.mu.Unlock()

	select {
	case b := <-m.readCh:
		n := copy(data, b)
		return n, nil
	case <-m.closeCh:
		return 0, io.EOF
	}
}

func (m *mockSSHChannel) Write(data []byte) (int, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	if m.closed {
		return 0, io.ErrClosedPipe
	}
	m.writeBuf = append(m.writeBuf, data...)
	return len(data), nil
}

func (m *mockSSHChannel) Close() error {
	m.mu.Lock()
	defer m.mu.Unlock()
	if !m.closed {
		m.closed = true
		close(m.closeCh)
	}
	return nil
}

func (m *mockSSHChannel) CloseWrite() error { return nil }

func (m *mockSSHChannel) SendRequest(name string, wantReply bool, payload []byte) (bool, error) {
	return false, nil
}

func (m *mockSSHChannel) Stderr() io.ReadWriter {
	return nil
}

func (m *mockSSHChannel) isClosed() bool {
	m.mu.Lock()
	defer m.mu.Unlock()
	return m.closed
}

// Ensure mockSSHChannel implements ssh.Channel.
var _ ssh.Channel = (*mockSSHChannel)(nil)

func TestChannelConnLocalAddr(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	addr := conn.LocalAddr()
	if addr.Network() != "tunnel" {
		t.Fatalf("expected Network()=='tunnel', got %q", addr.Network())
	}
	if addr.String() != "tunnel" {
		t.Fatalf("expected String()=='tunnel', got %q", addr.String())
	}
}

func TestChannelConnRemoteAddr(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	addr := conn.RemoteAddr()
	if addr.Network() != "tunnel" {
		t.Fatalf("expected Network()=='tunnel', got %q", addr.Network())
	}
	if addr.String() != "tunnel" {
		t.Fatalf("expected String()=='tunnel', got %q", addr.String())
	}
}

func TestChannelConnSetDeadlineClosesChannel(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	// Set a short deadline.
	err := conn.SetDeadline(time.Now().Add(50 * time.Millisecond))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Wait for the deadline to expire.
	time.Sleep(100 * time.Millisecond)

	if !ch.isClosed() {
		t.Fatal("expected channel to be closed after deadline expiration")
	}
}

func TestChannelConnSetDeadlineClearsTimer(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	// Set a deadline.
	err := conn.SetDeadline(time.Now().Add(50 * time.Millisecond))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Clear the deadline with zero time.
	err = conn.SetDeadline(time.Time{})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Wait past the original deadline.
	time.Sleep(100 * time.Millisecond)

	if ch.isClosed() {
		t.Fatal("expected channel to stay open after clearing deadline")
	}
}

func TestChannelConnSetDeadlineResetsTimer(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	// Set a short deadline.
	err := conn.SetDeadline(time.Now().Add(50 * time.Millisecond))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Reset to a longer deadline before the first expires.
	err = conn.SetDeadline(time.Now().Add(500 * time.Millisecond))
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Wait past the first deadline but before the second.
	time.Sleep(100 * time.Millisecond)

	if ch.isClosed() {
		t.Fatal("expected channel to stay open after resetting to longer deadline")
	}

	// Clean up.
	conn.SetDeadline(time.Time{})
}

func TestChannelConnReadDelegatesToChannel(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	expected := []byte("hello from channel")
	ch.readCh <- expected

	buf := make([]byte, 100)
	n, err := conn.Read(buf)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if string(buf[:n]) != string(expected) {
		t.Fatalf("expected %q, got %q", expected, buf[:n])
	}
}

func TestChannelConnWriteDelegatesToChannel(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	data := []byte("hello to channel")
	n, err := conn.Write(data)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if n != len(data) {
		t.Fatalf("expected %d bytes written, got %d", len(data), n)
	}

	ch.mu.Lock()
	defer ch.mu.Unlock()
	if string(ch.writeBuf) != string(data) {
		t.Fatalf("expected %q in write buffer, got %q", data, ch.writeBuf)
	}
}

func TestChannelConnSetWriteDeadlineIsNoop(t *testing.T) {
	ch := newMockChannel()
	conn := &channelConn{Channel: ch}

	err := conn.SetWriteDeadline(time.Now().Add(time.Second))
	if err != nil {
		t.Fatalf("expected nil error from SetWriteDeadline, got %v", err)
	}
}
