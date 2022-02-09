package tunnelstest

import (
	"context"
	"fmt"
	"net/http"
	"net/http/httptest"

	"github.com/gorilla/websocket"
	"golang.org/x/crypto/ssh"
)

const sshPrivateKey = `-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQC6VU6XsMaTot9ogsGcJ+juvJOmDvvCZmgJRTRwKkW0u2BLz4yV
rCzQcxaY4kaIuR80Y+1f0BLnZgh4pTREDR0T+p8hUsDSHim1ttKI8rK0hRtJ2qhY
lR4qt7P51rPA4KFA9z9gDjTwQLbDq21QMC4+n4d8CL3xRVGtlUAMM3Kl3wIDAQAB
AoGBAI8UemkYoSM06gBCh5D1RHQt8eKNltzL7g9QSNfoXeZOC7+q+/TiZPcbqLp0
5lyOalu8b8Ym7J0rSE377Ypj13LyHMXS63e4wMiXv3qOl3GDhMLpypnJ8PwqR2b8
IijL2jrpQfLu6IYqlteA+7e9aEexJa1RRwxYIyq6pG1IYpbhAkEA9nKgtj3Z6ZDC
46IdqYzuUM9ZQdcw4AFr407+lub7tbWe5pYmaq3cT725IwLw081OAmnWJYFDMa/n
IPl9YcZSPQJBAMGOMbPs/YPkQAsgNdIUlFtK3o41OrrwJuTRTvv0DsbqDV0LKOiC
t8oAQQvjisH6Ew5OOhFyIFXtvZfzQMJppksCQQDWFd+cUICTUEise/Duj9maY3Uz
J99ySGnTbZTlu8PfJuXhg3/d3ihrMPG6A1z3cPqaSBxaOj8H07mhQHn1zNU1AkEA
hkl+SGPrO793g4CUdq2ahIA8SpO5rIsDoQtq7jlUq0MlhGFCv5Y5pydn+bSjx5MV
933kocf5kUSBntPBIWElYwJAZTm5ghu0JtSE6t3km0iuj7NGAQSdb6mD8+O7C3CP
FU3vi+4HlBysaT6IZ/HG+/dBsr4gYp4LGuS7DbaLuYw/uw==
-----END RSA PRIVATE KEY-----`

type RelayServer struct {
	httpServer *httptest.Server
	errc       chan error
	sshConfig  *ssh.ServerConfig
}

func NewRelayServer() (*RelayServer, error) {
	server := &RelayServer{
		errc: make(chan error),
		sshConfig: &ssh.ServerConfig{
			NoClientAuth: true,
		},
	}

	privateKey, err := ssh.ParsePrivateKey([]byte(sshPrivateKey))
	if err != nil {
		return nil, fmt.Errorf("error parsing private key: %w", err)
	}
	server.sshConfig.AddHostKey(privateKey)

	server.httpServer = httptest.NewServer(http.HandlerFunc(makeConnection(server)))
	return server, nil
}

func (rs *RelayServer) URL() string {
	return rs.httpServer.URL
}

func (rs *RelayServer) Err() <-chan error {
	return rs.errc
}

func (rs *RelayServer) sendError(err error) {
	select {
	case rs.errc <- err:
	default:
		// channel is blocked with a previous error, so we ignore this one
	}
}

var upgrader = websocket.Upgrader{}

func makeConnection(server *RelayServer) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		ctx, cancel := context.WithCancel(context.Background())
		defer cancel()

		c, err := upgrader.Upgrade(w, r, nil)
		if err != nil {
			server.sendError(fmt.Errorf("error upgrading to websocket: %w", err))
			return
		}
		defer func() {
			if err := c.Close(); err != nil {
				server.sendError(fmt.Errorf("error closing websocket: %w", err))
			}
		}()

		socketConn := newSocketConn(c)
		_, chans, reqs, err := ssh.NewServerConn(socketConn, server.sshConfig)
		if err != nil {
			server.sendError(fmt.Errorf("error creating ssh server conn: %w", err))
			return
		}
		go ssh.DiscardRequests(reqs)

		if err := handleChannels(ctx, server, chans); err != nil {
			server.sendError(fmt.Errorf("error handling channels: %w", err))
			return
		}
	}
}

func handleChannels(ctx context.Context, server *RelayServer, chans <-chan ssh.NewChannel) error {
	errc := make(chan error, 1)
	go func() {
		for ch := range chans {
			ch, reqs, err := ch.Accept()
			if err != nil {
				errc <- fmt.Errorf("error accepting channel: %w", err)
				return
			}

			// TODO(josebalius): handle ch & reqs
			fmt.Println(ch, reqs)
		}
	}()
	return awaitError(ctx, errc)
}

func awaitError(ctx context.Context, errc <-chan error) error {
	select {
	case <-ctx.Done():
		return ctx.Err()
	case err := <-errc:
		return err
	}
}
