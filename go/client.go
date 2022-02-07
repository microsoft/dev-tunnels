package tunnels

import (
	"context"
	"io"
	"net"
)

type Client struct{}

func Connect(ctx context.Context, tunnel *Tunnel, hostID string) (*Client, error) {
	return nil, nil
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
