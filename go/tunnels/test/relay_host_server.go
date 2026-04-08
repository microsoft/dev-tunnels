// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelstest

import (
	"bytes"
	"fmt"
	"net"
	"net/http"
	"net/http/httptest"
	"sync"
	"time"

	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
	"github.com/gorilla/websocket"
	"golang.org/x/crypto/ssh"
)

// RelayHostServer is a WebSocket-level mock relay server for host tests.
// It validates the full WebSocket upgrade + SSH handshake, handles
// tcpip-forward/cancel-tcpip-forward global requests (V2), and can simulate
// client connections by opening forwarded-tcpip channels (V2) or
// client-ssh-session-stream channels (V1).
type RelayHostServer struct {
	httpServer  *httptest.Server
	accessToken string
	forceV1     bool
	errc        chan error

	mu                 sync.Mutex
	sshConn            ssh.Conn
	ports              map[uint16]struct{}
	connected          chan struct{}
	negotiatedProtocol string
}

// RelayHostServerOption is a functional option for configuring RelayHostServer.
type RelayHostServerOption func(*RelayHostServer)

// WithHostAccessToken configures the expected access token for the relay.
func WithHostAccessToken(token string) RelayHostServerOption {
	return func(s *RelayHostServer) {
		s.accessToken = token
	}
}

// WithProtocolV1Only forces the relay to negotiate V1 even if V2 is offered.
func WithProtocolV1Only() RelayHostServerOption {
	return func(s *RelayHostServer) {
		s.forceV1 = true
	}
}

// NewRelayHostServer creates a new WebSocket-level mock relay server for host tests.
func NewRelayHostServer(opts ...RelayHostServerOption) (*RelayHostServer, error) {
	server := &RelayHostServer{
		errc:      make(chan error, 1),
		connected: make(chan struct{}),
		ports:     make(map[uint16]struct{}),
	}

	for _, opt := range opts {
		opt(server)
	}

	server.httpServer = httptest.NewServer(http.HandlerFunc(server.handleConnection))

	return server, nil
}

// URL returns the WebSocket URL for the host to connect to.
func (s *RelayHostServer) URL() string {
	return "ws" + s.httpServer.URL[4:] // convert http:// to ws://
}

// NegotiatedProtocol returns the subprotocol selected during the WebSocket handshake.
func (s *RelayHostServer) NegotiatedProtocol() string {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.negotiatedProtocol
}

// SimulateClientConnection opens a forwarded-tcpip channel to the host with
// V2 extra data and returns the channel as a net.Conn.
func (s *RelayHostServer) SimulateClientConnection(port uint16) (net.Conn, error) {
	s.mu.Lock()
	conn := s.sshConn
	s.mu.Unlock()

	if conn == nil {
		return nil, fmt.Errorf("relay not connected")
	}

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

	channel, reqs, err := conn.OpenChannel("forwarded-tcpip", extraData)
	if err != nil {
		return nil, fmt.Errorf("error opening forwarded-tcpip channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	return &channelNetConn{Channel: channel}, nil
}

// SimulateClientConnectionV1 opens a client-ssh-session-stream channel (V1)
// and performs a nested SSH client handshake. Returns the *ssh.Client.
func (s *RelayHostServer) SimulateClientConnectionV1() (*ssh.Client, error) {
	s.mu.Lock()
	conn := s.sshConn
	s.mu.Unlock()

	if conn == nil {
		return nil, fmt.Errorf("relay not connected")
	}

	channel, reqs, err := conn.OpenChannel("client-ssh-session-stream", nil)
	if err != nil {
		return nil, fmt.Errorf("error opening client-ssh-session-stream channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	// Wrap as net.Conn for nested SSH handshake.
	chanConn := &channelNetConn{Channel: channel}

	clientConfig := &ssh.ClientConfig{
		User:            "tunnel",
		HostKeyCallback: ssh.InsecureIgnoreHostKey(),
		Timeout:         10 * time.Second,
	}

	sshConn, chans, globalReqs, err := ssh.NewClientConn(chanConn, "", clientConfig)
	if err != nil {
		channel.Close()
		return nil, fmt.Errorf("nested SSH client handshake failed: %w", err)
	}

	return ssh.NewClient(sshConn, chans, globalReqs), nil
}

// HasPort reports whether the given port has been registered via tcpip-forward.
func (s *RelayHostServer) HasPort(port uint16) bool {
	s.mu.Lock()
	defer s.mu.Unlock()
	_, ok := s.ports[port]
	return ok
}

// Err returns the error channel for the relay server.
func (s *RelayHostServer) Err() <-chan error {
	return s.errc
}

// Close shuts down the server.
func (s *RelayHostServer) Close() error {
	s.mu.Lock()
	conn := s.sshConn
	s.mu.Unlock()

	if conn != nil {
		conn.Close()
	}
	s.httpServer.Close()
	return nil
}

func (s *RelayHostServer) sendError(err error) {
	select {
	case s.errc <- err:
	default:
	}
}

var hostUpgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool { return true },
}

func (s *RelayHostServer) handleConnection(w http.ResponseWriter, r *http.Request) {
	// Validate access token if configured.
	if s.accessToken != "" {
		if r.Header.Get("Authorization") != s.accessToken {
			s.sendError(fmt.Errorf("invalid access token"))
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
	}

	// Select subprotocol from offered list.
	protocols := websocket.Subprotocols(r)
	selectedProtocol := ""
	if s.forceV1 {
		// Force V1: only accept tunnel-relay-host.
		for _, p := range protocols {
			if p == "tunnel-relay-host" {
				selectedProtocol = p
				break
			}
		}
	} else {
		// Prefer V2, fall back to V1.
		for _, p := range protocols {
			if p == "tunnel-relay-host-v2-dev" {
				selectedProtocol = p
				break
			}
		}
		if selectedProtocol == "" {
			for _, p := range protocols {
				if p == "tunnel-relay-host" {
					selectedProtocol = p
					break
				}
			}
		}
	}

	if selectedProtocol == "" {
		s.sendError(fmt.Errorf("no supported subprotocol offered: %v", protocols))
		http.Error(w, "bad subprotocol", http.StatusBadRequest)
		return
	}

	// Upgrade to WebSocket.
	respHeader := http.Header{}
	respHeader.Set("Sec-WebSocket-Protocol", selectedProtocol)
	c, err := hostUpgrader.Upgrade(w, r, respHeader)
	if err != nil {
		s.sendError(fmt.Errorf("error upgrading to websocket: %w", err))
		return
	}

	socketConn := newSocketConn(c)

	// Connect as SSH server to the host's SSH client.
	sshConfig := &ssh.ServerConfig{
		NoClientAuth: true,
	}
	privateKey, err := ssh.ParsePrivateKey([]byte(sshPrivateKey))
	if err != nil {
		s.sendError(fmt.Errorf("error parsing private key: %w", err))
		return
	}
	sshConfig.AddHostKey(privateKey)

	serverConn, _, reqs, err := ssh.NewServerConn(socketConn, sshConfig)
	if err != nil {
		s.sendError(fmt.Errorf("error creating SSH server conn: %w", err))
		return
	}

	// Handle global requests based on protocol.
	if selectedProtocol == "tunnel-relay-host" {
		// V1: reject all global requests (relay doesn't handle tcpip-forward).
		go func() {
			for req := range reqs {
				req.Reply(false, nil)
			}
		}()
	} else {
		// V2: handle tcpip-forward/cancel-tcpip-forward.
		go s.handleGlobalRequests(reqs)
	}

	s.mu.Lock()
	s.sshConn = serverConn
	s.negotiatedProtocol = selectedProtocol
	// Signal that the connection is established.
	select {
	case <-s.connected:
		// Already closed (reconnect scenario) — make a new channel.
		s.connected = make(chan struct{})
	default:
	}
	close(s.connected)
	s.mu.Unlock()

	// Block until connection closes.
	serverConn.Wait()
}

// handleGlobalRequests handles tcpip-forward and cancel-tcpip-forward from the host.
func (s *RelayHostServer) handleGlobalRequests(reqs <-chan *ssh.Request) {
	for req := range reqs {
		switch req.Type {
		case "tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			s.mu.Lock()
			s.ports[uint16(prr.Port())] = struct{}{}
			s.mu.Unlock()
			req.Reply(true, nil)

		case "cancel-tcpip-forward":
			var prr messages.PortRelayRequest
			if err := prr.Unmarshal(bytes.NewReader(req.Payload)); err != nil {
				req.Reply(false, nil)
				continue
			}
			s.mu.Lock()
			delete(s.ports, uint16(prr.Port()))
			s.mu.Unlock()
			req.Reply(true, nil)

		default:
			req.Reply(false, nil)
		}
	}
}

// WaitForConnection waits for the host to connect to the relay server.
func (s *RelayHostServer) WaitForConnection(timeout time.Duration) error {
	s.mu.Lock()
	ch := s.connected
	s.mu.Unlock()

	select {
	case <-ch:
		return nil
	case err := <-s.errc:
		return err
	case <-time.After(timeout):
		return fmt.Errorf("timeout waiting for host connection")
	}
}
