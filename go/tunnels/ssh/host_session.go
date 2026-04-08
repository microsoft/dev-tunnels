// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"log"
	"net"
	"regexp"
	"strconv"
	"sync"
	"sync/atomic"
	"time"

	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
	"golang.org/x/crypto/ssh"
)

// forwardedTCPIPData is the channel extra data for forwarded-tcpip and direct-tcpip channels.
type forwardedTCPIPData struct {
	Host       string
	Port       uint32
	OriginAddr string
	OriginPort uint32
}

const (
	sshUser       = "tunnel"
	localHostIPv4 = "127.0.0.1"
	localHostIPv6 = "[::1]"

	sshHandshakeTimeout = 10 * time.Second
	tcpDialTimeout      = 5 * time.Second

	// SshDisconnectReasonTooManyConnections is the SSH disconnect reason code
	// sent by the relay when too many host connections are active.
	SshDisconnectReasonTooManyConnections uint32 = 11

	// HostWebSocketSubProtocol is the V1 WebSocket subprotocol for host relay connections.
	HostWebSocketSubProtocol = "tunnel-relay-host"

	// HostWebSocketSubProtocolV2 is the V2 WebSocket subprotocol for host relay connections.
	HostWebSocketSubProtocolV2 = "tunnel-relay-host-v2-dev"

	// ClientStreamChannelType is the SSH channel type used in V1 for client session streams.
	ClientStreamChannelType = "client-ssh-session-stream"
)

// disconnectReasonRe matches the x/crypto/ssh disconnect message format.
var disconnectReasonRe = regexp.MustCompile(`ssh: disconnect, reason (\d+):`)

// HostSSHSession manages the SSH connection from the host to the relay.
//
// In V2 (tunnel-relay-host-v2-dev), the host sends tcpip-forward requests
// directly to the relay (with an access token), and the relay opens
// forwarded-tcpip channels directly to the host — no nested SSH sessions.
//
// In V1 (tunnel-relay-host), the relay opens client-ssh-session-stream
// channels. Each channel carries a nested SSH connection from a client.
// The host performs a nested SSH server handshake on each channel, then
// forwards ports to each client individually via tcpip-forward requests
// on the nested connection.
//
// Locking strategy: connMu, portsMu, and clientsMu guard independent
// state and are never held simultaneously.
type HostSSHSession struct {
	conn               net.Conn
	hostKey            ssh.Signer
	logger             *log.Logger
	accessToken        string
	connectionProtocol string

	// connMu guards sshConn and disconnectReason.
	connMu           sync.Mutex
	sshConn          ssh.Conn
	disconnectReason uint32

	portsMu sync.RWMutex
	ports   []uint16

	// V1 per-client SSH connections.
	clientsMu      sync.RWMutex
	clients        map[string]*ssh.ServerConn
	clientCounter  uint32 // atomic
}

// NewHostSSHSession creates a new HostSSHSession.
func NewHostSSHSession(conn net.Conn, hostKey ssh.Signer, logger *log.Logger, accessToken string, connectionProtocol string) *HostSSHSession {
	return &HostSSHSession{
		conn:               conn,
		hostKey:            hostKey,
		logger:             logger,
		accessToken:        accessToken,
		connectionProtocol: connectionProtocol,
		clients:            make(map[string]*ssh.ServerConn),
	}
}

// ConnectionProtocol returns the negotiated WebSocket subprotocol.
func (s *HostSSHSession) ConnectionProtocol() string {
	return s.connectionProtocol
}

// Connect establishes the SSH client connection to the relay.
func (s *HostSSHSession) Connect(ctx context.Context) error {
	clientConfig := ssh.ClientConfig{
		User:            sshUser,
		Timeout:         sshHandshakeTimeout,
		HostKeyCallback: ssh.InsecureIgnoreHostKey(),
	}

	sshConn, chans, reqs, err := ssh.NewClientConn(s.conn, "", &clientConfig)
	if err != nil {
		return fmt.Errorf("error creating SSH client connection to relay: %w", err)
	}

	s.connMu.Lock()
	s.sshConn = sshConn
	s.connMu.Unlock()

	go s.handleGlobalRequests(reqs)
	go s.handleIncomingChannels(ctx, chans)

	return nil
}

// Wait blocks until the relay SSH connection drops.
func (s *HostSSHSession) Wait() error {
	s.connMu.Lock()
	conn := s.sshConn
	s.connMu.Unlock()

	if conn == nil {
		return fmt.Errorf("not connected")
	}

	err := conn.Wait()

	s.connMu.Lock()
	s.disconnectReason = parseDisconnectReason(err)
	s.connMu.Unlock()

	return err
}

// Close closes the SSH connection to the relay.
func (s *HostSSHSession) Close() error {
	s.connMu.Lock()
	conn := s.sshConn
	s.connMu.Unlock()

	// Close all V1 client connections.
	s.clientsMu.Lock()
	for id, client := range s.clients {
		client.Close()
		delete(s.clients, id)
	}
	s.clientsMu.Unlock()

	if conn == nil {
		return nil
	}
	return conn.Close()
}

// DisconnectReason returns the SSH disconnect reason code from the last
// disconnection, or 0 if not disconnected or reason unknown.
func (s *HostSSHSession) DisconnectReason() uint32 {
	s.connMu.Lock()
	defer s.connMu.Unlock()
	return s.disconnectReason
}

// parseDisconnectReason extracts the SSH disconnect reason code from an error
// returned by x/crypto/ssh. Returns 0 if the reason cannot be determined.
func parseDisconnectReason(err error) uint32 {
	if err == nil {
		return 0
	}
	m := disconnectReasonRe.FindStringSubmatch(err.Error())
	if len(m) < 2 {
		return 0
	}
	reason, convErr := strconv.ParseUint(m[1], 10, 32)
	if convErr != nil {
		return 0
	}
	return uint32(reason)
}

// handleGlobalRequests rejects unknown global requests from the relay.
func (s *HostSSHSession) handleGlobalRequests(reqs <-chan *ssh.Request) {
	for r := range reqs {
		r.Reply(false, nil)
	}
}

// handleIncomingChannels dispatches incoming channels from the relay
// based on channel type and connection protocol.
func (s *HostSSHSession) handleIncomingChannels(ctx context.Context, chans <-chan ssh.NewChannel) {
	for newChan := range chans {
		switch newChan.ChannelType() {
		case ClientStreamChannelType:
			if s.connectionProtocol == HostWebSocketSubProtocol {
				go s.handleClientSession(ctx, newChan)
			} else {
				newChan.Reject(ssh.UnknownChannelType, "unknown channel type")
			}
		case "forwarded-tcpip", "direct-tcpip":
			go s.handleV2Channel(ctx, newChan)
		default:
			newChan.Reject(ssh.UnknownChannelType, "unknown channel type")
		}
	}
}

// handleClientSession handles a V1 client-ssh-session-stream channel.
// It accepts the channel, wraps it as a net.Conn, performs a nested SSH
// server handshake, forwards existing ports, and handles client channels.
func (s *HostSSHSession) handleClientSession(ctx context.Context, newChan ssh.NewChannel) {
	channel, reqs, err := newChan.Accept()
	if err != nil {
		s.logger.Printf("failed to accept client-ssh-session-stream: %v", err)
		return
	}
	go ssh.DiscardRequests(reqs)

	// Wrap the channel as a net.Conn for the nested SSH handshake.
	conn := &channelConn{Channel: channel}

	// Nested SSH server config — NoClientAuth since the relay already authenticated.
	serverConfig := &ssh.ServerConfig{
		NoClientAuth: true,
	}
	serverConfig.AddHostKey(s.hostKey)

	serverConn, clientChans, clientReqs, err := ssh.NewServerConn(conn, serverConfig)
	if err != nil {
		s.logger.Printf("nested SSH handshake failed: %v", err)
		channel.Close()
		return
	}

	// Generate a unique client ID and store the connection.
	clientID := fmt.Sprintf("client-%d", atomic.AddUint32(&s.clientCounter, 1))
	s.clientsMu.Lock()
	s.clients[clientID] = serverConn
	s.clientsMu.Unlock()

	s.logger.Printf("V1 client connected: %s", clientID)

	// Forward existing ports to this client.
	s.portsMu.RLock()
	currentPorts := make([]uint16, len(s.ports))
	copy(currentPorts, s.ports)
	s.portsMu.RUnlock()

	for _, port := range currentPorts {
		s.forwardPortToClient(serverConn, port)
	}

	// Handle global requests from the client (tcpip-forward, cancel-tcpip-forward).
	go s.handleClientGlobalRequests(clientReqs)

	// Handle channels from the client.
	go s.handleClientChannels(ctx, serverConn, clientChans)

	// Wait for the client connection to close, then clean up.
	serverConn.Wait()

	s.clientsMu.Lock()
	delete(s.clients, clientID)
	s.clientsMu.Unlock()

	s.logger.Printf("V1 client disconnected: %s", clientID)
}

// handleClientGlobalRequests handles global requests from a V1 client.
// In V1, clients may send tcpip-forward requests which we accept.
func (s *HostSSHSession) handleClientGlobalRequests(reqs <-chan *ssh.Request) {
	for req := range reqs {
		switch req.Type {
		case "tcpip-forward":
			// Accept port forward requests from clients.
			req.Reply(true, nil)
		case "cancel-tcpip-forward":
			req.Reply(true, nil)
		default:
			req.Reply(false, nil)
		}
	}
}

// forwardPortToClient sends a tcpip-forward request to a V1 client's
// nested SSH connection, notifying the client that a port is available.
func (s *HostSSHSession) forwardPortToClient(serverConn *ssh.ServerConn, port uint16) {
	pfr := messages.NewPortForwardRequest(localHostIPv4, uint32(port))
	b, err := pfr.Marshal()
	if err != nil {
		s.logger.Printf("error marshaling port forward request for port %d: %v", port, err)
		return
	}

	_, _, err = serverConn.SendRequest(messages.PortForwardRequestType, true, b)
	if err != nil {
		s.logger.Printf("error sending tcpip-forward to client for port %d: %v", port, err)
	}
}

// cancelForwardPortToClient sends a cancel-tcpip-forward request to a V1
// client's nested SSH connection.
func (s *HostSSHSession) cancelForwardPortToClient(serverConn *ssh.ServerConn, port uint16) {
	pfcr := messages.NewPortForwardCancelRequest(localHostIPv4, uint32(port))
	b, err := pfcr.Marshal()
	if err != nil {
		s.logger.Printf("error marshaling cancel port forward request for port %d: %v", port, err)
		return
	}

	_, _, err = serverConn.SendRequest(messages.PortForwardCancelRequestType, true, b)
	if err != nil {
		s.logger.Printf("error sending cancel-tcpip-forward to client for port %d: %v", port, err)
	}
}

// handleClientChannels dispatches channels from a V1 client's nested SSH connection.
func (s *HostSSHSession) handleClientChannels(ctx context.Context, serverConn *ssh.ServerConn, chans <-chan ssh.NewChannel) {
	for newChan := range chans {
		switch newChan.ChannelType() {
		case "forwarded-tcpip", "direct-tcpip":
			// Parse the standard forwarded-tcpip extra data to get the port.
			var data forwardedTCPIPData
			if err := ssh.Unmarshal(newChan.ExtraData(), &data); err != nil {
				newChan.Reject(ssh.ConnectionFailed, "invalid channel data")
				continue
			}

			if !s.HasPort(uint16(data.Port)) {
				s.logger.Printf("V1 client: rejected %s to unregistered port %d", newChan.ChannelType(), data.Port)
				newChan.Reject(ssh.Prohibited, "port not registered")
				continue
			}

			go s.handleForwardedTCPIP(ctx, uint16(data.Port), newChan)
		default:
			newChan.Reject(ssh.UnknownChannelType, "unknown channel type")
		}
	}
}

// handleV2Channel handles a forwarded-tcpip or direct-tcpip channel from
// the V2 relay. It parses the V2 extra data (with access token and E2E
// encryption flag), validates the port, and proxies to local TCP.
func (s *HostSSHSession) handleV2Channel(ctx context.Context, newChan ssh.NewChannel) {
	// Parse channel extra data. Try V2 format first (has additional fields),
	// fall back to standard forwarded-tcpip format.
	extraData := newChan.ExtraData()
	var port uint32

	var v2Data messages.PortRelayConnectRequest
	if err := v2Data.Unmarshal(bytes.NewReader(extraData)); err == nil {
		port = v2Data.Port
	} else {
		// Fall back to standard format.
		var data forwardedTCPIPData
		if err := ssh.Unmarshal(extraData, &data); err != nil {
			newChan.Reject(ssh.ConnectionFailed, "invalid channel data")
			return
		}
		port = data.Port
	}

	if !s.HasPort(uint16(port)) {
		s.logger.Printf("rejected %s to unregistered port %d", newChan.ChannelType(), port)
		newChan.Reject(ssh.Prohibited, "port not registered")
		return
	}

	s.handleForwardedTCPIP(ctx, uint16(port), newChan)
}

// HasPort reports whether the given port is in the forwarded ports list.
func (s *HostSSHSession) HasPort(port uint16) bool {
	s.portsMu.RLock()
	defer s.portsMu.RUnlock()
	for _, p := range s.ports {
		if p == port {
			return true
		}
	}
	return false
}

// Ports returns a copy of the currently registered port list.
func (s *HostSSHSession) Ports() []uint16 {
	s.portsMu.RLock()
	defer s.portsMu.RUnlock()
	result := make([]uint16, len(s.ports))
	copy(result, s.ports)
	return result
}

// handleForwardedTCPIP proxies a forwarded-tcpip or direct-tcpip channel
// to a local TCP port.
func (s *HostSSHSession) handleForwardedTCPIP(ctx context.Context, port uint16, newChan ssh.NewChannel) {
	channel, reqs, err := newChan.Accept()
	if err != nil {
		s.logger.Printf("failed to accept forwarded-tcpip channel for port %d: %v", port, err)
		return
	}
	go ssh.DiscardRequests(reqs)

	// Dial local TCP: try IPv4 first, fall back to IPv6.
	var tcpConn net.Conn
	dialer := net.Dialer{Timeout: tcpDialTimeout}
	tcpConn, err = dialer.DialContext(ctx, "tcp4", fmt.Sprintf("%s:%d", localHostIPv4, port))
	if err != nil {
		tcpConn, err = dialer.DialContext(ctx, "tcp6", fmt.Sprintf("%s:%d", localHostIPv6, port))
		if err != nil {
			s.logger.Printf("failed to dial local port %d: %v", port, err)
			channel.Close()
			return
		}
	}

	// Bidirectional io.Copy between the SSH channel and the TCP connection.
	// When one direction finishes, close the write side of the destination
	// to propagate EOF, then wait for the reverse direction to drain.
	errs := make(chan error, 2)

	go func() {
		_, err := io.Copy(tcpConn, channel)
		// Signal EOF to the local service so it stops reading.
		if tc, ok := tcpConn.(*net.TCPConn); ok {
			tc.CloseWrite()
		}
		errs <- err
	}()

	go func() {
		_, err := io.Copy(channel, tcpConn)
		channel.CloseWrite()
		errs <- err
	}()

	// Wait for both directions to complete or context cancellation.
	select {
	case <-ctx.Done():
	case <-errs:
		// One direction done — wait for the other to drain.
		select {
		case <-ctx.Done():
		case <-errs:
		}
	}

	channel.Close()
	tcpConn.Close()
}

// AddPort adds a port to the port list and notifies the relay or connected clients.
// If the port is already registered, this is a no-op.
//
// In V2, sends a tcpip-forward global request to the relay with the access token.
// In V1, iterates connected clients and sends tcpip-forward to each.
func (s *HostSSHSession) AddPort(port uint16, accessToken string) {
	s.portsMu.Lock()
	for _, p := range s.ports {
		if p == port {
			s.portsMu.Unlock()
			return
		}
	}
	s.ports = append(s.ports, port)
	s.portsMu.Unlock()

	if s.connectionProtocol == HostWebSocketSubProtocol {
		// V1: notify each connected client.
		s.clientsMu.RLock()
		clients := make([]*ssh.ServerConn, 0, len(s.clients))
		for _, c := range s.clients {
			clients = append(clients, c)
		}
		s.clientsMu.RUnlock()

		for _, client := range clients {
			s.forwardPortToClient(client, port)
		}
		return
	}

	// V2: send tcpip-forward to the relay.
	s.connMu.Lock()
	conn := s.sshConn
	s.connMu.Unlock()

	if conn == nil {
		return
	}

	prr := messages.NewPortRelayRequest(localHostIPv4, uint32(port), accessToken)
	b, err := prr.Marshal()
	if err != nil {
		s.logger.Printf("error marshaling port relay request for port %d: %v", port, err)
		return
	}

	_, _, err = conn.SendRequest(messages.PortForwardRequestType, true, b)
	if err != nil {
		s.logger.Printf("error sending tcpip-forward for port %d: %v", port, err)
	}
}

// RemovePort removes a port from the port list and notifies the relay or connected clients.
//
// In V2, sends a cancel-tcpip-forward global request to the relay.
// In V1, iterates connected clients and sends cancel-tcpip-forward to each.
func (s *HostSSHSession) RemovePort(port uint16, accessToken string) {
	s.portsMu.Lock()
	found := false
	for i, p := range s.ports {
		if p == port {
			s.ports = append(s.ports[:i], s.ports[i+1:]...)
			found = true
			break
		}
	}
	s.portsMu.Unlock()

	if !found {
		return
	}

	if s.connectionProtocol == HostWebSocketSubProtocol {
		// V1: notify each connected client.
		s.clientsMu.RLock()
		clients := make([]*ssh.ServerConn, 0, len(s.clients))
		for _, c := range s.clients {
			clients = append(clients, c)
		}
		s.clientsMu.RUnlock()

		for _, client := range clients {
			s.cancelForwardPortToClient(client, port)
		}
		return
	}

	// V2: send cancel-tcpip-forward to the relay.
	s.connMu.Lock()
	conn := s.sshConn
	s.connMu.Unlock()

	if conn == nil {
		return
	}

	prr := messages.NewPortRelayRequest(localHostIPv4, uint32(port), accessToken)
	b, err := prr.Marshal()
	if err != nil {
		s.logger.Printf("error marshaling cancel port relay request for port %d: %v", port, err)
		return
	}

	_, _, err = conn.SendRequest(messages.PortForwardCancelRequestType, true, b)
	if err != nil {
		s.logger.Printf("error sending cancel-tcpip-forward for port %d: %v", port, err)
	}
}
