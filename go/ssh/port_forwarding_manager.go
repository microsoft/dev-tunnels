package tunnelssh

import (
	"bytes"
	"context"
	"errors"
	"fmt"

	"github.com/microsoft/tunnels/go/ssh/messages"
	"golang.org/x/crypto/ssh"
)

const (
	portFrowardRequestType        = "tcpip-forward"
	cancelPortForwardRequestType  = "cancel-tcpip-forward"
	PortForwardChannelType        = "forwarded-tcpip"
	ReversePortForwardChannelType = "direct-tcpip"
	hostWebSocketSubProtocol      = "tunnel-relay-host"
	clientStreamChannelType       = "client-ssh-session-stream"
	loopbackIP                    = "127.0.0.1"
)

type forwardTask func() error

type portForwardingManagerService struct {
	disposed                                    bool
	localForwarders                             map[string]string
	remoteConnectors                            map[string]string
	channelForwarders                           []string
	forwardRequestTask                          forwardTask
	acceptLocalConnectionsForForwardedPorts     bool
	acceptRemoteConnectionsForNonForwardedPorts bool
	localForwardedPorts                         *ForwardedPorts
	remoteForwardedPorts                        *ForwardedPorts
	tcpListenerFactory                          string
}

func ForwardToRemotePort() error {
	return nil
}

func (pfm *portForwardingManagerService) forwardFromRemotePort(
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
	if pfm.localForwardedPorts.HasPort(localPort) {
		return false, fmt.Errorf("local port %d is already forwarded", localPort)
	} else if pfm.localForwardedPorts.HasPort(remotePort) {
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
		pfm.localForwardedPorts.Add(int(response.Port()))
	}

	return result, nil
}

func WaitForForwardedPort() error {
	return nil
}

func OnSessionRequest() error {
	return nil
}

func StartForwarding() (int, error) {
	return 0, nil
}

func CancelForwarding() (bool, error) {
	return false, nil
}

func OnChannelOpening() error {
	return nil
}

func OpenChannel() error {
	return nil
}

func AddChannelForwarder() error {
	return nil
}

func RemoveChannelForwarder() error {
	return nil
}
