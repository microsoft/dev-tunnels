package tunnelssh

import (
	"context"
	"fmt"
	"io"
	"net"
	"time"

	"golang.org/x/crypto/ssh"
)

type SSHSession struct {
	*ssh.Session
	socket net.Conn
	conn   ssh.Conn
	reader io.Reader
	writer io.Writer
}

func NewSSHSession(socket net.Conn) *SSHSession {
	return &SSHSession{socket: socket}
}

func (s *SSHSession) Connect(ctx context.Context) error {
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

	sshClientConn, chans, reqs, err := ssh.NewClientConn(s.socket, "", &clientConfig)
	if err != nil {
		return fmt.Errorf("error creating ssh client connection: %w", err)
	}
	s.conn = sshClientConn

	sshClient := ssh.NewClient(sshClientConn, chans, reqs)
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

func (s *SSHSession) Read(p []byte) (n int, err error) {
	return s.reader.Read(p)
}

func (s *SSHSession) Write(p []byte) (n int, err error) {
	return s.writer.Write(p)
}

func (s *SSHSession) OpenChannel(ctx context.Context, channelType string, data []byte) (ssh.Channel, error) {
	channel, reqs, err := s.conn.OpenChannel(channelType, data)
	if err != nil {
		return nil, fmt.Errorf("failed to open channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	return channel, nil
}
