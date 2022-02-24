package tunnels

import (
	"context"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"encoding/base64"
	"fmt"

	"github.com/google/uuid"
	tunnelssh "github.com/microsoft/tunnels/go/ssh"
)

type Host struct {
	manager     *Manager
	sshSessions map[tunnelssh.SSHSession]bool
	tunnel      *Tunnel
	privateKey  *rsa.PrivateKey
	hostId      string
}

func NewHost(manager *Manager) (*Host, error) {
	privateKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return nil, fmt.Errorf("private key could not be generated: %w", err)
	}
	return &Host{
		manager:     manager,
		sshSessions: make(map[tunnelssh.SSHSession]bool),
		privateKey:  privateKey,
		hostId:      uuid.New().String(),
	}, nil
}

func (h *Host) StartServer(ctx context.Context, tunnel *Tunnel) error {
	if tunnel == nil {
		return fmt.Errorf("tunnel cannot be nil")
	}
	if tunnel.Ports == nil {
		return fmt.Errorf("tunnel ports slice cannot be nil")
	}
	publicKeyBytes, err := x509.MarshalPKIXPublicKey(h.privateKey.PublicKey)
	if err != nil {
		return fmt.Errorf("error getting host public key: %w", err)
	}
	sEnc := base64.StdEncoding.EncodeToString(publicKeyBytes)
	hostPublicKeys := []string{sEnc}

	accessToken, ok := tunnel.AccessTokens[TunnelAccessScopeHost]
	if !ok {
		return fmt.Errorf("tunnel did not contain the host access token")
	}

	endpoint := &TunnelEndpoint{
		HostID:         h.hostId,
		HostPublicKeys: hostPublicKeys,
	}

	endpoint, err = h.manager.UpdateTunnelEndpoint(ctx, tunnel, endpoint, nil)
	if err != nil {
		return fmt.Errorf("error updating tunnel endpoint: %w", err)
	}

	if endpoint.HostRelayURI == "" {
		return fmt.Errorf("endpoint relay uri was not correctly set")
	}

	stream := SteamFactory.CreateRelayStream(
		endpoint.HostRelayURI,
		accessToken,
		"tunnel-relay-host",
	)
	return nil
}

func (h *Host) AddPort(ctx context.Context, port TunnelPort) (*TunnelPort, error) {
	return nil, nil
}

func (h *Host) RemovePort(ctx context.Context, portNumber int) (bool, error) {
	return false, nil
}

func (h *Host) UpdatePort(ctx context.Context, port TunnelPort) (*TunnelPort, error) {
	return nil, nil
}
