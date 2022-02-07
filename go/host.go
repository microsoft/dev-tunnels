package tunnels

import "context"

type Host struct {
	manager *Manager
}

func NewHost(manager *Manager) *Host {
	return &Host{}
}

func (h *Host) StartServer(ctx context.Context, tunnel *Tunnel, hostPublicKeys []string) error {
	return nil
}
