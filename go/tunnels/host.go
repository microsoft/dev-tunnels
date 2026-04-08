// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"encoding/base64"
	"errors"
	"fmt"
	"io"
	"log"
	"net/http"
	"strings"
	"sync"

	tunnelssh "github.com/microsoft/dev-tunnels/go/tunnels/ssh"
	"golang.org/x/crypto/ssh"
)

const (
	hostWebSocketSubProtocol   = "tunnel-relay-host"
	hostWebSocketSubProtocolV2 = "tunnel-relay-host-v2-dev"
)

var (
	// ErrNoManager is returned when no manager is provided.
	ErrNoManager = errors.New("manager cannot be nil")

	// ErrNoHostRelayURI is returned when the endpoint has no host relay URI.
	ErrNoHostRelayURI = errors.New("endpoint host relay URI is empty")

	// ErrAlreadyConnected is returned when the host is already connected.
	ErrAlreadyConnected = errors.New("host is already connected")

	// ErrPortAlreadyAdded is returned when the port is already forwarded.
	ErrPortAlreadyAdded = errors.New("port is already added")

	// ErrNotConnected is returned when the host is not connected.
	ErrNotConnected = errors.New("host is not connected")

	// ErrTooManyConnections is returned when the relay rejected the host
	// because another host is already connected to this tunnel.
	ErrTooManyConnections = errors.New("too many connections to tunnel")
)

// Host is a host for a tunnel. It is used to host a tunnel and forward
// local TCP ports to remote clients through the relay.
//
// Locking strategy: mu guards ssh, tunnel, connectionStatus, disconnectReason,
// ctx, and cancel. All locking uses the snapshot-under-lock pattern: acquire
// lock, copy values to locals, release, then operate on the locals.
type Host struct {
	logger  *log.Logger
	manager *Manager

	// mu guards ssh, tunnel, connectionStatus, disconnectReason, ctx, and cancel.
	mu               sync.Mutex
	tunnel           *Tunnel
	ssh              *tunnelssh.HostSSHSession
	connectionStatus ConnectionStatus
	disconnectReason uint32

	hostID     string
	endpointID string
	relayURI   string

	hostKey ssh.Signer

	// EnableReconnect enables automatic reconnection when the relay
	// connection drops. Default is false for backward compatibility.
	EnableReconnect bool

	// ConnectionStatusChanged is called when the connection status changes.
	// Both the previous and current status are provided.
	ConnectionStatusChanged func(prev, curr ConnectionStatus)

	// RefreshTunnelAccessTokenFunc is called to obtain a fresh access token
	// when the current one expires (HTTP 401). If nil, the host falls back
	// to re-fetching the tunnel from the management service.
	RefreshTunnelAccessTokenFunc func(ctx context.Context) (string, error)

	// ctx and cancel are created in Connect and cancelled in Close
	// to stop reconnection loops.
	ctx    context.Context
	cancel context.CancelFunc
}

// NewHost creates a new Host instance.
func NewHost(logger *log.Logger, manager *Manager) (*Host, error) {
	if manager == nil {
		return nil, ErrNoManager
	}

	if logger == nil {
		logger = log.New(io.Discard, "", 0)
	}

	hostID, err := generateUUID()
	if err != nil {
		return nil, fmt.Errorf("error generating host ID: %w", err)
	}

	_, privateKey, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		return nil, fmt.Errorf("error generating host key: %w", err)
	}

	signer, err := ssh.NewSignerFromKey(privateKey)
	if err != nil {
		return nil, fmt.Errorf("error creating SSH signer: %w", err)
	}

	h := &Host{
		logger:     logger,
		manager:    manager,
		hostID:     hostID,
		endpointID: fmt.Sprintf("%s-relay", hostID),
		hostKey:    signer,
	}
	return h, nil
}

// HostPublicKeyBase64 returns the base64-encoded public key of the host.
func (h *Host) HostPublicKeyBase64() string {
	return base64.StdEncoding.EncodeToString(h.hostKey.PublicKey().Marshal())
}

// ConnectionStatus returns the current connection status.
func (h *Host) ConnectionStatus() ConnectionStatus {
	h.mu.Lock()
	defer h.mu.Unlock()
	return h.connectionStatus
}

// setConnectionStatus updates the connection status and invokes the callback.
func (h *Host) setConnectionStatus(status ConnectionStatus) {
	h.mu.Lock()
	prev := h.connectionStatus
	h.connectionStatus = status
	cb := h.ConnectionStatusChanged
	h.mu.Unlock()

	if cb != nil && prev != status {
		cb(prev, status)
	}
}

// Connect connects the host to a tunnel relay.
func (h *Host) Connect(ctx context.Context, tunnel *Tunnel) error {
	if ctx == nil {
		ctx = context.Background()
	}

	h.mu.Lock()
	if h.ssh != nil {
		h.mu.Unlock()
		return ErrAlreadyConnected
	}
	if h.disconnectReason == tunnelssh.SshDisconnectReasonTooManyConnections {
		h.mu.Unlock()
		return ErrTooManyConnections
	}
	h.tunnel = tunnel
	h.mu.Unlock()

	connCtx, connCancel := context.WithCancel(ctx)
	h.mu.Lock()
	h.ctx = connCtx
	h.cancel = connCancel
	h.mu.Unlock()

	return h.connectOnce(ctx, tunnel)
}

// connectOnce performs a single connection attempt: endpoint registration,
// WebSocket connect, and SSH session setup.
func (h *Host) connectOnce(ctx context.Context, tunnel *Tunnel) error {
	h.setConnectionStatus(ConnectionStatusConnecting)

	// Check if any port uses the "ssh" protocol for the gateway key query param.
	var opts *TunnelRequestOptions
	for _, p := range tunnel.Ports {
		if p.Protocol == "ssh" {
			opts = &TunnelRequestOptions{
				AdditionalQueryParameters: map[string]string{
					"includeSshGatewayPublicKey": "true",
				},
			}
			break
		}
	}

	// Register the endpoint with the management API.
	endpoint := &TunnelEndpoint{
		ID:             h.endpointID,
		HostID:         h.hostID,
		ConnectionMode: TunnelConnectionModeTunnelRelay,
		HostPublicKeys: []string{h.HostPublicKeyBase64()},
	}

	endpointResult, err := h.manager.UpdateTunnelEndpoint(ctx, tunnel, endpoint, nil, opts)
	if err != nil {
		h.setConnectionStatus(ConnectionStatusDisconnected)
		return fmt.Errorf("error updating tunnel endpoint: %w", err)
	}

	if endpointResult.HostRelayURI == "" {
		h.setConnectionStatus(ConnectionStatusDisconnected)
		return ErrNoHostRelayURI
	}
	h.relayURI = endpointResult.HostRelayURI

	// Extract host access token, guarding against nil map.
	var accessToken string
	if tunnel.AccessTokens != nil {
		accessToken = tunnel.AccessTokens[TunnelAccessScopeHost]
	}

	h.logger.Printf("Connecting to host tunnel relay %s", h.relayURI)
	protocols := []string{hostWebSocketSubProtocolV2, hostWebSocketSubProtocol}

	var headers http.Header
	if accessToken != "" {
		headers = make(http.Header)
		if !strings.HasPrefix(accessToken, "Tunnel ") {
			accessToken = fmt.Sprintf("Tunnel %s", accessToken)
		}
		headers.Add("Authorization", accessToken)
	}

	sock := newSocket(h.relayURI, protocols, headers, nil)
	if err := sock.connect(ctx); err != nil {
		h.setConnectionStatus(ConnectionStatusDisconnected)
		return fmt.Errorf("error connecting to host relay: %w", err)
	}

	negotiatedProtocol := sock.Subprotocol()
	h.logger.Printf("Negotiated subprotocol: %s", negotiatedProtocol)

	// In V1, the relay does not handle tcpip-forward; pass empty token.
	sshAccessToken := accessToken
	if negotiatedProtocol == hostWebSocketSubProtocol {
		sshAccessToken = ""
	}

	sshSession := tunnelssh.NewHostSSHSession(sock, h.hostKey, h.logger, sshAccessToken, negotiatedProtocol)
	if err := sshSession.Connect(ctx); err != nil {
		sock.Close()
		h.setConnectionStatus(ConnectionStatusDisconnected)
		return fmt.Errorf("error establishing SSH session: %w", err)
	}

	h.mu.Lock()
	h.ssh = sshSession
	h.mu.Unlock()

	h.setConnectionStatus(ConnectionStatusConnected)
	return nil
}

// Close gracefully shuts down the host connection.
// It closes the SSH session and unregisters the endpoint.
// Close is idempotent — calling it twice does not panic or error.
// Returns ErrNotConnected if the host was never connected.
func (h *Host) Close() error {
	h.mu.Lock()
	sshSession := h.ssh
	tunnel := h.tunnel
	cancel := h.cancel
	h.ssh = nil
	h.mu.Unlock()

	// Cancel any reconnection loop.
	if cancel != nil {
		cancel()
	}

	if sshSession == nil {
		// If tunnel is set, we were connected before — this is an idempotent close.
		if tunnel != nil {
			return nil
		}
		return ErrNotConnected
	}

	h.setConnectionStatus(ConnectionStatusDisconnected)

	sshSession.Close()

	// Unregister the endpoint unless the relay disconnected us for
	// TooManyConnections (another host is authoritative).
	if tunnel != nil && sshSession.DisconnectReason() != tunnelssh.SshDisconnectReasonTooManyConnections {
		ctx := context.Background()
		if err := h.manager.DeleteTunnelEndpoints(ctx, tunnel, h.endpointID, nil); err != nil {
			h.logger.Printf("error deleting tunnel endpoint: %v", err)
		}
	}

	return nil
}

// Wait blocks until the relay connection drops.
// If EnableReconnect is true, Wait will attempt to reconnect with
// exponential backoff before returning.
func (h *Host) Wait() error {
	h.mu.Lock()
	sshSession := h.ssh
	h.mu.Unlock()

	if sshSession == nil {
		return ErrNotConnected
	}

	for {
		err := sshSession.Wait()

		h.mu.Lock()
		h.disconnectReason = sshSession.DisconnectReason()
		disconnectReason := h.disconnectReason
		reconnect := h.EnableReconnect
		connCtx := h.ctx
		h.mu.Unlock()

		h.setConnectionStatus(ConnectionStatusDisconnected)

		if !reconnect {
			return err
		}

		if disconnectReason == tunnelssh.SshDisconnectReasonTooManyConnections {
			return ErrTooManyConnections
		}

		if reconnectErr := h.reconnect(connCtx); reconnectErr != nil {
			return reconnectErr
		}

		// Reconnected — read new session and wait again.
		h.mu.Lock()
		sshSession = h.ssh
		h.mu.Unlock()

		if sshSession == nil {
			return ErrNotConnected
		}
	}
}

// AddPort registers a port with the management API and notifies connected clients.
func (h *Host) AddPort(ctx context.Context, port *TunnelPort) error {
	h.mu.Lock()
	sshSession := h.ssh
	tunnel := h.tunnel
	h.mu.Unlock()

	if sshSession == nil {
		return ErrNotConnected
	}

	if sshSession.HasPort(port.PortNumber) {
		return ErrPortAlreadyAdded
	}

	// Register the port with the management API.
	_, err := h.manager.CreateTunnelPort(ctx, tunnel, port, nil)
	if err != nil {
		// Tolerate 409 Conflict (port already exists on the service).
		var tunnelErr *TunnelError
		if !errors.As(err, &tunnelErr) || tunnelErr.StatusCode != http.StatusConflict {
			return fmt.Errorf("error creating tunnel port: %w", err)
		}
	}

	// Extract access token for the relay request.
	// V1 does not send tokens to the relay; V2 requires them.
	var accessToken string
	if sshSession.ConnectionProtocol() != tunnelssh.HostWebSocketSubProtocol {
		if tunnel.AccessTokens != nil {
			accessToken = tunnel.AccessTokens[TunnelAccessScopeHost]
		}
	}

	// Add to SSH session and send tcpip-forward to the relay (V2) or clients (V1).
	sshSession.AddPort(port.PortNumber, accessToken)

	return nil
}

// RemovePort removes a forwarded port and notifies connected clients.
func (h *Host) RemovePort(ctx context.Context, portNumber uint16) error {
	h.mu.Lock()
	sshSession := h.ssh
	tunnel := h.tunnel
	h.mu.Unlock()

	if sshSession == nil {
		return ErrNotConnected
	}

	// Unregister from the management API. Errors are logged but not returned.
	if err := h.manager.DeleteTunnelPort(ctx, tunnel, portNumber, nil); err != nil {
		h.logger.Printf("error deleting tunnel port %d: %v", portNumber, err)
	}

	// Extract access token for the relay request.
	// V1 does not send tokens to the relay; V2 requires them.
	var accessToken string
	if sshSession.ConnectionProtocol() != tunnelssh.HostWebSocketSubProtocol {
		if tunnel.AccessTokens != nil {
			accessToken = tunnel.AccessTokens[TunnelAccessScopeHost]
		}
	}

	sshSession.RemovePort(portNumber, accessToken)

	return nil
}

// RefreshPorts synchronizes the local forwarded ports with the tunnel service.
// New ports on the service are added, and stale local ports are removed.
func (h *Host) RefreshPorts(ctx context.Context) error {
	h.mu.Lock()
	sshSession := h.ssh
	tunnel := h.tunnel
	h.mu.Unlock()

	if sshSession == nil {
		return ErrNotConnected
	}

	// Fetch tunnel with ports from the service.
	opts := &TunnelRequestOptions{IncludePorts: true}
	refreshed, err := h.manager.GetTunnel(ctx, tunnel, opts)
	if err != nil {
		return fmt.Errorf("error fetching tunnel for port refresh: %w", err)
	}

	// Build a set of remote port numbers.
	remotePorts := make(map[uint16]struct{}, len(refreshed.Ports))
	for _, p := range refreshed.Ports {
		remotePorts[p.PortNumber] = struct{}{}
	}

	// Get current local ports from the SSH session (single source of truth).
	localPorts := sshSession.Ports()
	localSet := make(map[uint16]struct{}, len(localPorts))
	for _, pn := range localPorts {
		localSet[pn] = struct{}{}
	}

	// Extract access token for the relay requests.
	// V1 does not send tokens to the relay; V2 requires them.
	var accessToken string
	if sshSession.ConnectionProtocol() != tunnelssh.HostWebSocketSubProtocol {
		if tunnel.AccessTokens != nil {
			accessToken = tunnel.AccessTokens[TunnelAccessScopeHost]
		}
	}

	// Add ports that are on the service but not local.
	for pn := range remotePorts {
		if _, exists := localSet[pn]; !exists {
			sshSession.AddPort(pn, accessToken)
		}
	}

	// Remove ports that are local but not on the service.
	for _, pn := range localPorts {
		if _, exists := remotePorts[pn]; !exists {
			sshSession.RemovePort(pn, accessToken)
		}
	}

	// Update tunnel reference with refreshed ports.
	h.mu.Lock()
	h.tunnel.Ports = refreshed.Ports
	h.mu.Unlock()

	return nil
}

// generateUUID generates a UUID v4 string using crypto/rand.
func generateUUID() (string, error) {
	var uuid [16]byte
	if _, err := io.ReadFull(rand.Reader, uuid[:]); err != nil {
		return "", err
	}
	// Set version (4) and variant (RFC 4122)
	uuid[6] = (uuid[6] & 0x0f) | 0x40
	uuid[8] = (uuid[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x",
		uuid[0:4], uuid[4:6], uuid[6:8], uuid[8:10], uuid[10:16]), nil
}
