package tunnelssh

import (
	"context"
	"fmt"
	"log"
	"net"
	"time"

	"golang.org/x/crypto/ssh"
)

type HostSSHSession struct {
	*SSHSession

	supportedChannelTypes             []string
	supportedChannelNotificationChans map[string]<-chan ssh.NewChannel
}

func NewHostSSHSession(socket net.Conn, pf portForwardingManager, supportedChannelTypes []string, logger *log.Logger) *HostSSHSession {
	return &HostSSHSession{
		SSHSession: &SSHSession{
			socket: socket,
			logger: logger,
		},
		supportedChannelTypes:             supportedChannelTypes,
		supportedChannelNotificationChans: make(map[string]<-chan ssh.NewChannel),
	}
}

func (s *HostSSHSession) Connect(ctx context.Context) error {
	clientConfig := ssh.ClientConfig{
		// For now, the client is allowed to skip SSH authentication;
		// they must have a valid tunnel access token already to get this far.
		User:    "tunnel",
		Timeout: 10 * time.Second,

		// TODO: Validate host public keys match those published to the service?
		// For now, the assumption is only a host with access to the tunnel can get a token
		// that enables listening for tunnel connections.
		HostKeyCallback: ssh.InsecureIgnoreHostKey(),
	}

	// This is where the host currently breaks due to a mismatch of key exchange algorithms
	sshClientConn, chans, reqs, err := ssh.NewClientConn(s.socket, "", &clientConfig)
	if err != nil {
		return fmt.Errorf("error creating ssh client connection: %w", err)
	}
	s.conn = sshClientConn

	sshClient := ssh.NewClient(sshClientConn, chans, reqs)
	for _, channelType := range s.supportedChannelTypes {
		s.supportedChannelNotificationChans[channelType] = sshClient.HandleChannelOpen(channelType)
	}

	s.Session, err = sshClient.NewSession()
	if err != nil {
		return fmt.Errorf("error creating ssh client session: %w", err)
	}

	s.reader, err = s.Session.StdoutPipe()
	if err != nil {
		return fmt.Errorf("error creating ssh session reader: %w", err)
	}

	s.writer, err = s.Session.StdinPipe()
	if err != nil {
		return fmt.Errorf("error creating ssh session writer: %w", err)
	}

	return nil
}

func (s *HostSSHSession) OpenChannelNotifier(channelType string) <-chan ssh.NewChannel {
	return s.supportedChannelNotificationChans[channelType]
}
