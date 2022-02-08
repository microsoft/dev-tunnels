package tunnels

import (
	"context"
	"fmt"
	"io"
	"net"
	"time"

	"golang.org/x/crypto/ssh"
)

type sshSession struct {
	*ssh.Session
	socket net.Conn
	conn   ssh.Conn
	reader io.Reader
	writer io.Writer
}

func newSSHSession(socket net.Conn) *sshSession {
	return &sshSession{socket: socket}
}

func (s *sshSession) connect(ctx context.Context) error {
	clientConfig := ssh.ClientConfig{
		User:    "tunnel",
		Timeout: 10 * time.Second,
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

func (s *sshSession) Read(p []byte) (n int, err error) {
	return s.reader.Read(p)
}

func (s *sshSession) Write(p []byte) (n int, err error) {
	return s.writer.Write(p)
}
