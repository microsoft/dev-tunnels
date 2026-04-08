// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelstest

import (
	"bytes"
	"fmt"
	"net"
	"sync"
	"time"

	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
	"golang.org/x/crypto/ssh"
)

// MockRelayForHost simulates a V2 relay server using net.Pipe() for host unit tests.
// It connects as an SSH server to the host's SSH client on the relay end of the pipe.
// In V2, the relay handles tcpip-forward/cancel-tcpip-forward global requests and
// opens forwarded-tcpip channels directly to the host (no nested SSH).
type MockRelayForHost struct {
	sshConn ssh.Conn

	mu    sync.Mutex
	ports map[uint16]struct{}
}

// NewMockRelayForHost creates a new MockRelayForHost that connects as an SSH server
// to the host on the given net.Conn (typically the relay end of a net.Pipe()).
func NewMockRelayForHost(relayEnd net.Conn) (*MockRelayForHost, error) {
	sshConfig := &ssh.ServerConfig{
		NoClientAuth: true,
	}

	privateKey, err := ssh.ParsePrivateKey([]byte(sshPrivateKey))
	if err != nil {
		return nil, fmt.Errorf("error parsing private key: %w", err)
	}
	sshConfig.AddHostKey(privateKey)

	serverConn, chans, reqs, err := ssh.NewServerConn(relayEnd, sshConfig)
	if err != nil {
		return nil, fmt.Errorf("error creating SSH server connection: %w", err)
	}

	m := &MockRelayForHost{
		sshConn: serverConn,
		ports:   make(map[uint16]struct{}),
	}

	go m.handleGlobalRequests(reqs)

	// Drain incoming channels (reject all).
	go func() {
		for ch := range chans {
			ch.Reject(ssh.Prohibited, "not supported")
		}
	}()

	return m, nil
}

// handleGlobalRequests handles tcpip-forward and cancel-tcpip-forward from the host.
func (m *MockRelayForHost) handleGlobalRequests(reqs <-chan *ssh.Request) {
	for req := range reqs {
		switch req.Type {
		case "tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			m.mu.Lock()
			m.ports[uint16(prr.Port())] = struct{}{}
			m.mu.Unlock()
			req.Reply(true, nil)

		case "cancel-tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			m.mu.Lock()
			delete(m.ports, uint16(prr.Port()))
			m.mu.Unlock()
			req.Reply(true, nil)

		default:
			req.Reply(false, nil)
		}
	}
}

// SimulateClientConnection opens a forwarded-tcpip channel to the host with
// V2 extra data and returns the channel as a net.Conn for test client use.
func (m *MockRelayForHost) SimulateClientConnection(port uint16) (net.Conn, error) {
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
		return nil, fmt.Errorf("error marshaling V2 channel data: %w", err)
	}

	channel, reqs, err := m.sshConn.OpenChannel("forwarded-tcpip", extraData)
	if err != nil {
		return nil, fmt.Errorf("error opening forwarded-tcpip channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	return &channelNetConn{Channel: channel}, nil
}

// HasPort reports whether the given port has been registered via tcpip-forward.
func (m *MockRelayForHost) HasPort(port uint16) bool {
	m.mu.Lock()
	defer m.mu.Unlock()
	_, ok := m.ports[port]
	return ok
}

// Close cleanly shuts down the mock relay.
func (m *MockRelayForHost) Close() error {
	if m.sshConn != nil {
		m.sshConn.Close()
	}
	return nil
}

// channelNetConn wraps an ssh.Channel as a net.Conn for test use.
type channelNetConn struct {
	ssh.Channel
}

func (c *channelNetConn) LocalAddr() net.Addr                { return dummyTestAddr{} }
func (c *channelNetConn) RemoteAddr() net.Addr               { return dummyTestAddr{} }
func (c *channelNetConn) SetDeadline(t time.Time) error      { return nil }
func (c *channelNetConn) SetReadDeadline(t time.Time) error  { return nil }
func (c *channelNetConn) SetWriteDeadline(t time.Time) error { return nil }

type dummyTestAddr struct{}

func (dummyTestAddr) Network() string { return "tunnel-test" }
func (dummyTestAddr) String() string  { return "tunnel-test" }

// MockRelayForHostV1 simulates a V1 relay server using net.Pipe() for host unit tests.
// In V1, the relay rejects all global requests (no tcpip-forward handling) and
// simulates client connections by opening client-ssh-session-stream channels
// with a nested SSH client handshake inside.
type MockRelayForHostV1 struct {
	sshConn ssh.Conn
}

// NewMockRelayForHostV1 creates a new V1 mock relay that connects as an SSH server
// to the host on the given net.Conn.
func NewMockRelayForHostV1(relayEnd net.Conn) (*MockRelayForHostV1, error) {
	sshConfig := &ssh.ServerConfig{
		NoClientAuth: true,
	}

	privateKey, err := ssh.ParsePrivateKey([]byte(sshPrivateKey))
	if err != nil {
		return nil, fmt.Errorf("error parsing private key: %w", err)
	}
	sshConfig.AddHostKey(privateKey)

	serverConn, chans, reqs, err := ssh.NewServerConn(relayEnd, sshConfig)
	if err != nil {
		return nil, fmt.Errorf("error creating SSH server connection: %w", err)
	}

	m := &MockRelayForHostV1{
		sshConn: serverConn,
	}

	// V1 relay rejects all global requests.
	go func() {
		for req := range reqs {
			req.Reply(false, nil)
		}
	}()

	// Drain incoming channels (reject all).
	go func() {
		for ch := range chans {
			ch.Reject(ssh.Prohibited, "not supported")
		}
	}()

	return m, nil
}

// SimulateClientConnection opens a client-ssh-session-stream channel to the host,
// performs a nested SSH client handshake inside it, and returns the *ssh.Client
// so tests can receive tcpip-forward requests and open channels on the nested SSH.
func (m *MockRelayForHostV1) SimulateClientConnection() (*ssh.Client, error) {
	channel, reqs, err := m.sshConn.OpenChannel("client-ssh-session-stream", nil)
	if err != nil {
		return nil, fmt.Errorf("error opening client-ssh-session-stream channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	// Wrap channel as net.Conn for the nested SSH client handshake.
	conn := &channelNetConn{Channel: channel}

	// Perform nested SSH client handshake inside the channel.
	clientConfig := &ssh.ClientConfig{
		User:            "tunnel",
		HostKeyCallback: ssh.InsecureIgnoreHostKey(),
		Timeout:         10 * time.Second,
	}

	sshConn, chans, globalReqs, err := ssh.NewClientConn(conn, "", clientConfig)
	if err != nil {
		channel.Close()
		return nil, fmt.Errorf("nested SSH client handshake failed: %w", err)
	}

	client := ssh.NewClient(sshConn, chans, globalReqs)
	return client, nil
}

// Close cleanly shuts down the V1 mock relay.
func (m *MockRelayForHostV1) Close() error {
	if m.sshConn != nil {
		m.sshConn.Close()
	}
	return nil
}
