package tunnels

import (
	"context"

	"golang.org/x/crypto/ssh"
)

type hostServer struct {
	transport *serverTransport
}

func newHostServer(sock *socket, ch ssh.Channel) *hostServer {
	return &hostServer{
		transport: newServerTransport(sock, ch),
	}
}

func (h *hostServer) start(ctx context.Context) error {
	serverConn, chans, reqs, err := ssh.NewServerConn(newServerTransport(conn), &ssh.ServerConfig{
		// For now, the client is allowed to skip SSH authentication;
		// they must have a valid tunnel access token already to get this far.
		NoClientAuth: true,
		PublicKeyCallback: func(conn ssh.ConnMetadata, key ssh.PublicKey) (*ssh.Permissions, error) {
			// TODO(josebalius): check if the public key is in the host public keys
			return nil, nil
		},
	})

	// We have a successful authentication, send forwarded ports

	// Handle incoming channels

	return nil
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
