// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"strings"

	"net/http"

	tunnelssh "github.com/microsoft/dev-tunnels/go/tunnels/ssh"
	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
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
	remoteForwardedPorts *remoteForwardedPorts

	acceptLocalConnectionsForForwardedPorts bool
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
func NewClient(logger *log.Logger, tunnel *Tunnel, acceptLocalConnectionsForForwardedPorts bool) (*Client, error) {
	if tunnel == nil {
		return nil, ErrNoTunnel
	}

	if len(tunnel.Endpoints) == 0 {
		return nil, ErrNoTunnelEndpoints
	}

	c := &Client{
		logger:                                  logger,
		tunnel:                                  tunnel,
		endpoints:                               tunnel.Endpoints,
		remoteForwardedPorts:                    newRemoteForwardedPorts(),
		acceptLocalConnectionsForForwardedPorts: acceptLocalConnectionsForForwardedPorts,
	}
	return c, nil
}

func (c *Client) Connect(ctx context.Context, hostID string) error {
	endpointGroups := make(map[string][]TunnelEndpoint)
	for _, endpoint := range c.tunnel.Endpoints {
		endpointGroups[endpoint.HostID] = append(endpointGroups[endpoint.HostID], endpoint)
	}

	var endpointGroup []TunnelEndpoint
	c.hostID = hostID
	if hostID != "" {
		g, ok := endpointGroups[hostID]
		if !ok {
			return ErrNoConnections
		}
		endpointGroup = g
	} else if len(endpointGroups) > 1 {
		return ErrMultipleHosts
	} else {
		endpointGroup = endpointGroups[c.tunnel.Endpoints[0].HostID]
	}

	if len(c.endpoints) != 1 {
		return ErrNoRelayConnections
	}
	tunnelEndpoint := endpointGroup[0]
	clientRelayURI := tunnelEndpoint.ClientRelayURI

	accessToken := c.tunnel.AccessTokens[TunnelAccessScopeConnect]

	c.logger.Printf(fmt.Sprintf("Connecting to client tunnel relay %s", clientRelayURI))
	c.logger.Printf(fmt.Sprintf("Sec-Websocket-Protocol: %s", clientWebSocketSubProtocol))
	protocols := []string{clientWebSocketSubProtocol}

	var headers http.Header
	if accessToken != "" {
		headers = make(http.Header)
		if !strings.Contains(accessToken, "Tunnel") && !strings.Contains(accessToken, "tunnel") {
			accessToken = fmt.Sprintf("Tunnel %s", accessToken)
		}
		headers.Add("Authorization", accessToken)
		c.logger.Printf(fmt.Sprintf("Authorization: %s", accessToken))

	}

	sock := newSocket(clientRelayURI, protocols, headers, nil)
	if err := sock.connect(ctx); err != nil {
		return fmt.Errorf("failed to connect to client relay: %w", err)
	}

	c.ssh = tunnelssh.NewClientSSHSession(sock, c.remoteForwardedPorts, c.acceptLocalConnectionsForForwardedPorts, c.logger)
	if err := c.ssh.Connect(ctx); err != nil {
		return fmt.Errorf("failed to create ssh session: %w", err)
	}

	return nil
}

// ConnectListenerToForwardedPort opens a stream to a remote port and connects it to a given listener.
//
// Ensure that the port is already forwarded before calling this function
// by calling WaitForForwardedPort. Otherwise, this will return an error.
//
// Set acceptLocalConnectionsForForwardedPorts to false when creating the client to ensure
// TCP listeners are not created for all ports automatically when the client connects.
func (c *Client) ConnectListenerToForwardedPort(ctx context.Context, listenerIn net.Listener, port uint16) error {
	errc := make(chan error, 1)
	go func() {
		for {
			conn, err := listenerIn.Accept()
			if err != nil {
				sendError(err, errc)
				return
			}

			go func() {
				if err := c.ConnectToForwardedPort(ctx, conn, port); err != nil {
					sendError(err, errc)
				}
			}()
		}
	}()

	return awaitError(ctx, errc)
}

// ConnectToForwardedPort opens a stream to a remote port and connects it to a given connection.
//
// Ensure that the port is already forwarded before calling this function
// by calling WaitForForwardedPort. Otherwise, this will return an error.
//
// Set acceptLocalConnectionsForForwardedPorts to false when creating the client to ensure
// TCP listeners are not created for all ports automatically when the client connects.
func (c *Client) ConnectToForwardedPort(ctx context.Context, conn io.ReadWriteCloser, port uint16) error {
	errc := make(chan error, 1)
	go func() {
		if err := c.handleConnection(ctx, conn, port); err != nil {
			sendError(err, errc)
		}
	}()

	return awaitError(ctx, errc)
}

// WaitForForwardedPort waits for the specified port to be forwarded.
// It is common practice to call this function before ConnectToForwardedPort.
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

func (c *Client) RefreshPorts(ctx context.Context) error {
	if c.ssh == nil {
		return fmt.Errorf("not Connected")
	}

	res, _, err := c.ssh.SendSessionRequest("RefreshPorts", true, make([]byte, 0))
	if err != nil {
		return fmt.Errorf("failed to send port refresh message: %w", err)
	}
	if !res {
		return fmt.Errorf("failed to refresh ports: %w", err)
	}

	return err
}

func sendError(err error, errc chan error) {
	// Use non-blocking send, to avoid goroutines getting
	// stuck in case of concurrent or sequential errors.
	select {
	case errc <- err:
	default:
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

func (c *Client) openStreamingChannel(ctx context.Context, port uint16) (ssh.Channel, error) {
	portForwardChannel := messages.NewPortForwardChannel(
		c.ssh.NextChannelID(),
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

func (c *Client) Close() error {
	return c.ssh.Close()
}
