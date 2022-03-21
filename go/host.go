package tunnels

import (
	"bytes"
	"context"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"encoding/base64"
	"errors"
	"fmt"
	"log"
	"net/http"

	"github.com/google/uuid"
	tunnelssh "github.com/microsoft/tunnels/go/ssh"
	"github.com/microsoft/tunnels/go/ssh/messages"
	"golang.org/x/crypto/ssh"
	"golang.org/x/sync/errgroup"
)

const (
	hostWebSocketSubProtocol = "tunnel-relay-host"
	clientStreamChannelType  = "client-ssh-session-stream"
	loopbackIP               = "127.0.0.1"
)

type Host struct {
	manager             *Manager
	tunnel              *Tunnel
	privateKey          *rsa.PrivateKey
	publicKeys          []string
	hostId              string
	logger              *log.Logger
	ssh                 *tunnelssh.HostSSHSession
	sock                *socket
	localForwardedPorts *forwardedPorts
}

func NewHost(manager *Manager, logger *log.Logger) (*Host, error) {
	privateKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return nil, fmt.Errorf("private key could not be generated: %w", err)
	}
	return &Host{
		manager:             manager,
		privateKey:          privateKey,
		hostId:              uuid.New().String(),
		logger:              logger,
		localForwardedPorts: newForwardedPorts(),
	}, nil
}

// This must be called on an existing host and the tunnel and tunnel.ports cannot be nil
func (h *Host) StartServer(ctx context.Context, tunnel *Tunnel, hostPublicKeys []string) (err error) {
	// check input
	if tunnel == nil {
		return fmt.Errorf("tunnel cannot be nil")
	}
	h.tunnel = tunnel
	if tunnel.Ports == nil {
		return fmt.Errorf("tunnel ports slice cannot be nil")
	}

	// generate rsa keys
	if len(hostPublicKeys) == 0 {
		publicKeyBytes, err := x509.MarshalPKIXPublicKey(&h.privateKey.PublicKey)
		if err != nil {
			return fmt.Errorf("error getting host public key: %w", err)
		}
		sEnc := base64.StdEncoding.EncodeToString(publicKeyBytes)
		h.publicKeys = []string{sEnc}
	} else {
		h.publicKeys = hostPublicKeys
	}

	accessToken, ok := tunnel.AccessTokens[TunnelAccessScopeHost]
	if !ok {
		return fmt.Errorf("tunnel did not contain the host access token")
	}

	// create and publish the endpoint to the tunnel
	// this will return an endpoint with the hostRelayURI set
	endpoint := &TunnelEndpoint{
		HostID:         h.hostId,
		HostPublicKeys: h.publicKeys,
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

	h.sock = newSocket(hostRelayUri, protocols, headers, nil)
	if err := h.sock.connect(ctx); err != nil {
		return fmt.Errorf("failed to connect to host relay: %w", err)
	}

	supportedChannelTypes := []string{clientStreamChannelType}
	h.ssh = tunnelssh.NewHostSSHSession(h.sock, h.localForwardedPorts, supportedChannelTypes, h.logger)
	if err := h.ssh.Connect(ctx); err != nil {
		return fmt.Errorf("failed to create ssh session: %w", err)
	}

	g, ctx := errgroup.WithContext(ctx)
	for _, channelType := range supportedChannelTypes {
		chanType := channelType
		g.Go(func() error {
			ch := h.ssh.OpenChannelNotifier(chanType)
			return h.handleOpenChannel(ctx, ch)
		})
	}

	return g.Wait()
}

func sendError(ch chan error, err error) {
	select {
	case ch <- err:
	default:
	}
}

// TODO(josebalius): audit this method
func (h *Host) handleOpenChannel(ctx context.Context, incomingChannels <-chan ssh.NewChannel) error {
	errc := make(chan error, 1)

	go func() {
		for channel := range incomingChannels {
			if channel.ChannelType() != clientStreamChannelType {
				channel.Reject(ssh.UnknownChannelType, "unknown channel type")
				sendError(errc, fmt.Errorf("unknown channel type: %s", channel.ChannelType()))
				return
			}
			channelSession, requests, err := channel.Accept()
			if err != nil {
				sendError(errc, fmt.Errorf("failed to accept channel: %w", err))
				return
			}

			// TODO(josebalius): are these requests really discarded?
			go ssh.DiscardRequests(requests)

			innerChannel := channel
			go func() {
				h.logger.Println(fmt.Sprintf("accepted channel: %s", innerChannel.ChannelType()))
				if err := h.connectAndRunClientSession(ctx, channelSession); err != nil {
					sendError(errc, fmt.Errorf("failed to handle channel session: %w", err))
				}
			}()
		}
	}()

	return awaitError(ctx, errc)
}

func (h *Host) connectAndRunClientSession(ctx context.Context, channelSession ssh.Channel) error {
	hostServer := newHostServer(h, channelSession)
	return hostServer.start(ctx)
}

func (h *Host) forwardPort(ctx context.Context, session *ssh.ServerConn, port *TunnelPort) error {
	forwarded, err := h.forwardFromRemotePort(ctx, session, loopbackIP, port.PortNumber, loopbackIP, port.PortNumber)
	if err != nil {
		return fmt.Errorf("failed to forward port: %w", err)
	}
	fmt.Println(forwarded)

	// TODO(josebalius): what to do with the forwarded port?
	return nil
}

func (h *Host) forwardFromRemotePort(
	ctx context.Context, session *ssh.ServerConn, remoteIP string, remotePort int, localHost string, localPort int,
) (result bool, err error) {
	if localHost == "" {
		localHost = loopbackIP
	}
	if localPort == 0 {
		localPort = remotePort
	}
	if remoteIP == "" {
		return false, errors.New("remoteIP cannot be empty")
	}
	if localPort <= 0 {
		return false, errors.New("localPort must be a positive integer")
	}
	if h.localForwardedPorts.hasPort(localPort) {
		return false, fmt.Errorf("local port %d is already forwarded", localPort)
	} else if h.localForwardedPorts.hasPort(remotePort) {
		return false, fmt.Errorf("remote port %d is already forwarded", remotePort)
	}

	m := messages.NewPortForwardRequest(remoteIP, uint32(remotePort))
	b, err := m.Marshal()
	if err != nil {
		return false, fmt.Errorf("error marshaling port forward request: %w", err)
	}
	replied, payload, err := session.SendRequest(messages.PortForwardRequestType, true, b)
	if err != nil {
		return false, fmt.Errorf("error sending port forward request: %w", err)
	}
	if !replied {
		return false, fmt.Errorf("port forward request was not replied to")
	}

	response := new(messages.PortForwardSuccess)
	buf := bytes.NewBuffer(payload)
	if err := response.Unmarshal(buf); err != nil {
		return false, fmt.Errorf("error unmarshaling port forward success: %w", err)
	}

	if response.Port() != 0 {
		result = true
	}

	return result, nil
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
