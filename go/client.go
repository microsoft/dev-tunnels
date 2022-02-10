package tunnels

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
)

const (
	clientWebSocketSubProtocol = "tunnel-relay-client"
)

// Client is a client for a tunnel. It is used to connect to a tunnel.
type Client struct {
	logger *log.Logger

	hostID    string
	tunnel    *Tunnel
	endpoints []*TunnelEndpoint

	ssh *sshSession
}

var (
	// ErrNoTunnel is returned when no tunnel is provided.
	ErrNoTunnel = errors.New("tunnel cannot be nil")

	// ErrNoTunnelEndpoints is returned when no tunnel endpoints are provided.
	ErrNoTunnelEndpoints = errors.New("tunnel endpoints cannot be nil or empty")

	// ErrNoConnections is returned when no tunnel endpoints are provided for the given host ID.
	ErrNoConnections = errors.New("the specified host is not currently accepting connections to the tunnel")

	// ErrMultipleHosts is returned when multiple tunnel endpoints for different hosts are provided.
	ErrMultipleHosts = errors.New("there are multiple hosts for the tunnel, specify the host ID to connect to")

	// ErrNoRelayConnections is returned when no relay connections are available.
	ErrNoRelayConnections = errors.New("the host is not currently accepting tunnel relay connections")
)

// Connect connects to a tunnel and returns a connected client.
func Connect(ctx context.Context, logger *log.Logger, tunnel *Tunnel, hostID string) (*Client, error) {
	if tunnel == nil {
		return nil, ErrNoTunnel
	}

	if tunnel.Endpoints == nil || len(tunnel.Endpoints) == 0 {
		return nil, ErrNoTunnelEndpoints
	}

	endpointGroups := make(map[string][]*TunnelEndpoint)
	for _, endpoint := range tunnel.Endpoints {
		endpointGroups[endpoint.HostID] = append(endpointGroups[endpoint.HostID], endpoint)
	}

	var endpointGroup []*TunnelEndpoint
	if hostID != "" {
		g, ok := endpointGroups[hostID]
		if !ok {
			return nil, ErrNoConnections
		}
		endpointGroup = g
	} else if len(endpointGroups) > 1 {
		return nil, ErrMultipleHosts
	} else {
		endpointGroup = endpointGroups[tunnel.Endpoints[0].HostID]
	}

	c := &Client{logger: logger, hostID: hostID, tunnel: tunnel, endpoints: endpointGroup}
	return c.connect(ctx)
}

func (c *Client) connect(ctx context.Context) (*Client, error) {
	if len(c.endpoints) != 1 {
		return nil, ErrNoRelayConnections
	}
	tunnelEndpoint := c.endpoints[0]
	clientRelayURI := tunnelEndpoint.ClientRelayURI
	accessToken := c.tunnel.AccessTokens[TunnelAccessScopeConnect]

	c.logger.Println(fmt.Sprintf("Connecting to client tunnel relay %s", clientRelayURI))
	c.logger.Println(fmt.Sprintf("Sec-Websocket-Protocol: %s", clientWebSocketSubProtocol))
	protocols := []string{clientWebSocketSubProtocol}

	if accessToken != "" {
		c.logger.Println(fmt.Sprintf("Authorization: tunnel %s", accessToken))
		protocols = append(protocols, accessToken)
	}

	sock := newSocket(clientRelayURI, protocols, nil)
	if err := sock.connect(ctx); err != nil {
		return nil, fmt.Errorf("failed to connect to client relay: %w", err)
	}

	c.ssh = newSSHSession(sock)
	if err := c.ssh.connect(ctx); err != nil {
		return nil, fmt.Errorf("failed to create ssh session: %w", err)
	}

	return c, nil
}

func (s *Client) ForwardPort(ctx context.Context, port int, conn io.ReadWriteCloser) error {
	return nil
}

func (s *Client) ForwardPortToListener(ctx context.Context, port int, listener net.Listener) error {
	return nil
}

func (s *Client) WaitForForwardedPort(ctx context.Context, port int) error {
	return nil
}
