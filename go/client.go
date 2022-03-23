package tunnels

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"sync/atomic"

	"net/http"

	tunnelssh "github.com/microsoft/tunnels/go/ssh"
	"github.com/microsoft/tunnels/go/ssh/messages"
	"golang.org/x/crypto/ssh"
)

const (
	clientWebSocketSubProtocol = "tunnel-relay-client"
)

// Client is a client for a tunnel. It is used to connect to a tunnel.
type Client struct {
	logger *log.Logger

	hostID    string
	tunnel    *Tunnel
	endpoints []TunnelEndpoint

	ssh                  *tunnelssh.ClientSSHSession
	channels             uint32
	remoteForwardedPorts *remoteForwardedPorts
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

	// ErrSSHConnectionClosed is returned when the ssh connection is closed.
	ErrSSHConnectionClosed = errors.New("the ssh connection is closed")

	// ErrPortNotForwarded is returned when the specified port is not forwarded.
	ErrPortNotForwarded = errors.New("the port is not forwarded")
)

// Connect connects to a tunnel and returns a connected client.
func Connect(ctx context.Context, logger *log.Logger, tunnel *Tunnel, hostID string) (*Client, error) {
	if tunnel == nil {
		return nil, ErrNoTunnel
	}

	if tunnel.Endpoints == nil || len(*tunnel.Endpoints) == 0 {
		return nil, ErrNoTunnelEndpoints
	}

	endpointGroups := make(map[string][]TunnelEndpoint)
	for _, endpoint := range *tunnel.Endpoints {
		if (endpoint.HostID != nil) {
			endpointGroups[*endpoint.HostID] = append(endpointGroups[*endpoint.HostID], endpoint)
		}
	}

	var endpointGroup []TunnelEndpoint
	if hostID != "" {
		g, ok := endpointGroups[hostID]
		if !ok {
			return nil, ErrNoConnections
		}
		endpointGroup = g
	} else if len(endpointGroups) > 1 {
		return nil, ErrMultipleHosts
	} else if tunnel.Endpoints == nil || (*tunnel.Endpoints)[0].HostID == nil {
		return nil, ErrNoConnections
	} else {
		endpointGroup = endpointGroups[*(*tunnel.Endpoints)[0].HostID]
	}

	c := &Client{
		logger:               logger,
		hostID:               hostID,
		tunnel:               tunnel,
		endpoints:            endpointGroup,
		remoteForwardedPorts: newRemoteForwardedPorts(),
	}
	return c.connect(ctx)
}

func (c *Client) connect(ctx context.Context) (*Client, error) {
	if len(c.endpoints) != 1 {
		return nil, ErrNoRelayConnections
	}
	tunnelEndpoint := c.endpoints[0]

	if tunnelEndpoint.ClientRelayURI == nil {
		return nil, ErrNoRelayConnections
	}
	clientRelayURI := *tunnelEndpoint.ClientRelayURI

	var accessToken string
	if c.tunnel.AccessTokens != nil {
		accessToken = (*c.tunnel.AccessTokens)[TunnelAccessScopeConnect]
	}

	c.logger.Println(fmt.Sprintf("Connecting to client tunnel relay %s", clientRelayURI))
	c.logger.Println(fmt.Sprintf("Sec-Websocket-Protocol: %s", clientWebSocketSubProtocol))
	protocols := []string{clientWebSocketSubProtocol}

	var headers http.Header
	if accessToken != "" {
		c.logger.Println(fmt.Sprintf("Authorization: tunnel %s", accessToken))
		headers = make(http.Header)

		headers.Add("Authorization", fmt.Sprintf("tunnel %s", accessToken))
	}

	sock := newSocket(clientRelayURI, protocols, headers, nil)
	if err := sock.connect(ctx); err != nil {
		return nil, fmt.Errorf("failed to connect to client relay: %w", err)
	}

	c.ssh = tunnelssh.NewClientSSHSession(sock, c.remoteForwardedPorts, c.logger)
	if err := c.ssh.Connect(ctx); err != nil {
		return nil, fmt.Errorf("failed to create ssh session: %w", err)
	}

	return c, nil
}

// ConnectToForwardedPort connects to a forwarded port.
// It accepts a listener and will forward the connection to the tunnel.
// This only starts the process of forwarding the port, use WaitForForwardedPort to wait for the port to be forwarded.
func (c *Client) ConnectToForwardedPort(ctx context.Context, listener net.Listener, port uint16) error {
	if !c.remoteForwardedPorts.hasPort(port) {
		return ErrPortNotForwarded
	}

	errc := make(chan error, 1)
	sendError := func(err error) {
		// Use non-blocking send, to avoid goroutines getting
		// stuck in case of concurrent or sequential errors.
		select {
		case errc <- err:
		default:
		}
	}

	go func() {
		for {
			conn, err := listener.Accept()
			if err != nil {
				sendError(err)
				return
			}

			go func() {
				if err := c.handleConnection(ctx, conn, port); err != nil {
					sendError(err)
				}
			}()
		}
	}()

	return awaitError(ctx, errc)
}

// WaitForForwardedPort waits for the specified port to be forwarded.
func (c *Client) WaitForForwardedPort(ctx context.Context, port uint16) error {
	// It's already forwarded there's no need to wait.
	if c.remoteForwardedPorts.hasPort(port) {
		return nil
	}

	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case n := <-c.remoteForwardedPorts.notify:
			if n.port == port && n.notificationType == remoteForwardedPortNotificationTypeAdd {
				return nil
			}
		}
	}
}

func awaitError(ctx context.Context, errc chan error) error {
	select {
	case err := <-errc:
		return err
	case <-ctx.Done():
		return ctx.Err()
	}
}

func (c *Client) handleConnection(ctx context.Context, conn io.ReadWriteCloser, port uint16) (err error) {
	defer safeClose(conn, &err)

	channel, err := c.openStreamingChannel(ctx, port)
	if err != nil {
		return fmt.Errorf("failed to open streaming channel: %w", err)
	}

	// Ideally we would call safeClose again, but (*ssh.channel).Close
	// appears to have a bug that causes it return io.EOF spuriously
	// if its peer closed first; see github.com/golang/go/issues/38115.
	defer func() {
		closeErr := channel.Close()
		if err == nil && closeErr != io.EOF {
			err = closeErr
		}
	}()

	errs := make(chan error, 2)
	copyConn := func(w io.Writer, r io.Reader) {
		_, err := io.Copy(w, r)
		errs <- err
	}

	go copyConn(conn, channel)
	go copyConn(channel, conn)

	// Wait until context is cancelled or both copies are done.
	// Discard errors from io.Copy; they should not cause (e.g.) failures.
	for i := 0; ; {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-errs:
			i++
			if i == 2 {
				return nil
			}
		}
	}
}

func safeClose(c io.Closer, err *error) {
	if closerErr := c.Close(); *err == nil {
		*err = closerErr
	}
}

func (c *Client) nextChannelID() uint32 {
	return atomic.AddUint32(&c.channels, 1)
}

func (c *Client) openStreamingChannel(ctx context.Context, port uint16) (ssh.Channel, error) {
	portForwardChannel := messages.NewPortForwardChannel(
		c.nextChannelID(),
		"127.0.0.1",
		uint32(port),
		"",
		0,
	)
	data, err := portForwardChannel.Marshal()
	if err != nil {
		return nil, fmt.Errorf("failed to marshal port forward channel open message: %w", err)
	}

	channel, err := c.ssh.OpenChannel(ctx, portForwardChannel.Type(), data)
	if err != nil {
		return nil, fmt.Errorf("failed to open port forward channel: %w", err)
	}

	return channel, nil
}
