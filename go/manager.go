package tunnels

import "context"

type Manager struct{}

func NewManager() *Manager {
	return &Manager{}
}

type TunnelRequestOptions struct {
	AccessToken       string
	AdditionalHeaders map[string]string
	FollowRedirects   bool
	IncludePorts      bool
	Scopes            []string
	TokenScopes       []string
}

func (m *Manager) ListTunnels(
	ctx context.Context, clusterID string, options *TunnelRequestOptions,
) ([]*Tunnel, error) {
	return nil, nil
}

func (m *Manager) SearchTunnels(
	ctx context.Context, tags []string, requireAllTags bool, clusterID string, options *TunnelRequestOptions,
) ([]*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) GetTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) CreateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) UpdateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) error {
	return nil
}

func (m *Manager) UpdateTunnelEndpoint(
	ctx context.Context, tunnel *Tunnel, endpoint *TunnelEndpoint, options *TunnelRequestOptions,
) (*TunnelEndpoint, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnelEndpoints(
	ctx context.Context, tunnel *Tunnel, hostID string, connectionMode TunnelConnectionMode, options *TunnelRequestOptions,
) error {
	return nil
}

func (m *Manager) ListTunnelPorts(
	ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions,
) ([]*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) GetTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) CreateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) UpdateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) error {
	return nil
}
