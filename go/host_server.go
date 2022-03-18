package tunnels

import (
	"bytes"
	"context"
	"fmt"

	"github.com/microsoft/tunnels/go/ssh/messages"
	"golang.org/x/crypto/ssh"
)

type HostServer struct {
	host       *Host
	transport  *serverTransport
	serverConn *ssh.ServerConn
}

func newHostServer(h *Host, ch ssh.Channel) *HostServer {
	return &HostServer{
		host:      h,
		transport: newServerTransport(h.sock, ch),
	}
}

// TODO(josebalius): audit all go routines and ensure they are closed
// properly.
func (h *HostServer) start(ctx context.Context) error {
	errc := make(chan error, 1)
	serverConn, chans, reqs, err := ssh.NewServerConn(h.transport, &ssh.ServerConfig{
		// For now, the client is allowed to skip SSH authentication;
		// they must have a valid tunnel access token already to get this far.
		NoClientAuth: true,
		PublicKeyCallback: func(conn ssh.ConnMetadata, key ssh.PublicKey) (*ssh.Permissions, error) {
			// TODO(josebalius): check if the public key is in the host public keys
			return nil, nil
		},
	})
	if err != nil {
		return fmt.Errorf("failed to accept SSH connection: %w", err)
	}
	h.serverConn = serverConn

	// Handle global requests
	go func() {
		if err := h.handleRequests(ctx, reqs); err != nil {
			sendError(errc, err)
		}
	}()

	// We have a successful authentication, send forwarded ports
	for _, port := range h.host.tunnel.Ports {
		if port.PortNumber != 0 {
			if err := h.host.forwardPort(ctx, h.serverConn, port); err != nil {
				return fmt.Errorf("failed to forward port %d: %w", port.PortNumber, err)
			}
		}
	}

	// Handle incoming channels
	go func() {
		if err := h.handleChannels(ctx, chans); err != nil {
			sendError(errc, err)
		}
	}()

	return awaitError(ctx, errc)
}

func (h *HostServer) handleRequests(ctx context.Context, reqs <-chan *ssh.Request) error {
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case req, ok := <-reqs:
			if !ok {
				return nil
			}
			if err := h.handleRequest(ctx, req); err != nil {
				return err
			}
		}
	}
}

func (h *HostServer) handleRequest(ctx context.Context, req *ssh.Request) error {
	if req.Type != "tcpip-forward" && req.Type != "cancel-tcpip-forward" {
		return fmt.Errorf("unsupported request type: %s", req.Type)
	}

	m := new(messages.PortForwardRequest)
	if err := m.Unmarshal(bytes.NewBuffer(req.Payload)); err != nil {
		return fmt.Errorf("failed to unmarshal request payload: %w", err)
	}

	switch req.Type {
	case "tcpip-forward":
		// TODO(josebalius): handle tcpip-forward request
	case "cancel-tcpip-forward":
		// TODO(josebalius): handle cancel-tcpip-forward request
	}

	return nil
}

func (h *HostServer) handleChannels(ctx context.Context, chans <-chan ssh.NewChannel) error {
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case newChanReq, ok := <-chans:
			if !ok {
				return nil
			}

			channelType := newChanReq.ChannelType()
			switch channelType {
			case "direct-tcpip":
				go h.handleDirectTCPIP(ctx, newChanReq)
			case "forwarded-tcpip":
				go h.handleForwardedTCPIP(ctx, newChanReq)
			default:
				newChanReq.Reject(ssh.UnknownChannelType, "unknown channel type")
				continue
			}
		}
	}
}

func (h *HostServer) handleDirectTCPIP(ctx context.Context, newChanReq ssh.NewChannel) {
	var foundPort bool
	m := new(messages.PortForwardChannel)
	if err := m.Unmarshal(bytes.NewBuffer(newChanReq.ExtraData())); err != nil {
		newChanReq.Reject(ssh.ConnectionFailed, "invalid channel data")
		return
	}
	for _, port := range h.host.tunnel.Ports {
		if port.PortNumber == int(m.Port()) {
			foundPort = true
			break
		}
	}
	if !foundPort {
		newChanReq.Reject(ssh.Prohibited, "invalid port")
		return
	}
}

func (h *HostServer) handleForwardedTCPIP(ctx context.Context, newChanReq ssh.NewChannel) {
	// TODO(josebalius): implement
}

type serverTransport struct {
	*socket
	ssh.Channel
}

func newServerTransport(sock *socket, channel ssh.Channel) *serverTransport {
	return &serverTransport{
		socket:  sock,
		Channel: channel,
	}
}

func (s *serverTransport) Read(p []byte) (n int, err error) {
	return s.Channel.Read(p)
}

func (s *serverTransport) Write(p []byte) (n int, err error) {
	return s.Channel.Write(p)
}

func (s *serverTransport) Close() error {
	return s.Channel.Close()
}
