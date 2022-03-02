package tunnels

import (
	"context"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"encoding/base64"
	"fmt"
	"log"
	"net/http"

	"github.com/google/uuid"
	"github.com/gorilla/websocket"
	tunnelssh "github.com/microsoft/tunnels/go/ssh"
	"golang.org/x/crypto/ssh"
)

const (
	hostWebSocketSubProtocol = "tunnel-relay-host"
)

type RelayServer struct {
	httpServer  *http.Server
	errc        chan error
	sshConfig   *ssh.ServerConfig
	channels    map[string]channelHandler
	accessToken string

	serverConn *ssh.ServerConn
}

type RelayServerOption func(*RelayServer)
type channelHandler func(context.Context, ssh.NewChannel) error

type Host struct {
	manager          *Manager
	sshSessions      map[tunnelssh.SSHSession]bool
	remoteForwarders map[string]string
	tunnel           *Tunnel
	privateKey       *rsa.PrivateKey
	hostId           string
	logger           *log.Logger
	ssh              *tunnelssh.SSHSession
	server           *http.Server
}

func NewHost(manager *Manager, logger *log.Logger) (*Host, error) {
	privateKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return nil, fmt.Errorf("private key could not be generated: %w", err)
	}
	return &Host{
		manager:          manager,
		sshSessions:      make(map[tunnelssh.SSHSession]bool),
		remoteForwarders: make(map[string]string),
		privateKey:       privateKey,
		hostId:           uuid.New().String(),
		logger:           logger,
	}, nil
}

func (h *Host) StartServer(ctx context.Context, tunnel *Tunnel) error {
	if tunnel == nil {
		return fmt.Errorf("tunnel cannot be nil")
	}
	if tunnel.Ports == nil {
		return fmt.Errorf("tunnel ports slice cannot be nil")
	}
	publicKeyBytes, err := x509.MarshalPKIXPublicKey(&h.privateKey.PublicKey)
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
		ConnectionMode: TunnelConnectionModeTunnelRelay,
	}
	requestOptions := TunnelRequestOptions{}
	endpoint, err = h.manager.UpdateTunnelEndpoint(ctx, tunnel, endpoint, &requestOptions)
	if err != nil {
		return fmt.Errorf("error updating tunnel endpoint: %w", err)
	}

	if endpoint.HostRelayURI == "" {
		return fmt.Errorf("endpoint relay uri was not correctly set")
	}
	hostRelayUri := endpoint.HostRelayURI
	protocols := []string{hostWebSocketSubProtocol}

	var headers http.Header
	if accessToken != "" {
		h.logger.Println(fmt.Sprintf("Authorization: tunnel %s", accessToken))
		headers = make(http.Header)

		headers.Add("Authorization", fmt.Sprintf("tunnel %s", accessToken))
	}

	sock := newSocket(hostRelayUri, protocols, headers, nil)
	if err := sock.connect(ctx); err != nil {
		return fmt.Errorf("failed to connect to host relay: %w", err)
	}

	var upgrader = websocket.Upgrader{}

	makeConnection := func(server *RelayServer) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			ctx, cancel := context.WithCancel(context.Background())
			defer cancel()

			if server.accessToken != "" {
				if r.Header.Get("Authorization") != fmt.Sprintf("tunnel %s", server.accessToken) {
					server.sendError(fmt.Errorf("invalid access token"))
					return
				}
			}

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
			serverConn, chans, reqs, err := ssh.NewServerConn(socketConn, server.sshConfig)
			if err != nil {
				server.sendError(fmt.Errorf("error creating ssh server conn: %w", err))
				return
			}
			go ssh.DiscardRequests(reqs)

			server.serverConn = serverConn
			if err := handleChannels(ctx, server, chans); err != nil {
				server.sendError(fmt.Errorf("error handling channels: %w", err))
				return
			}
		}
	}

	h.server = &http.Server{}

	//serverConn, chans, reqs, err := ssh.NewServerConn(socketConn, server.sshConfig)

	//h.ssh = tunnelssh.NewSSHSession(sock, c.remoteForwardedPorts, h.logger)
	//if err := h.ssh.Connect(ctx); err != nil {
	//	return fmt.Errorf("failed to create ssh session: %w", err)
	//}

	return nil
}

func (rs *RelayServer) sendError(err error) {
	select {
	case rs.errc <- err:
	default:
		// channel is blocked with a previous error, so we ignore this one
	}
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
