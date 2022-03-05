package tunnelssh

import (
	"bytes"
	"context"
	"fmt"
	"log"
	"net"
	"time"

	"github.com/microsoft/tunnels/go/ssh/messages"
	"golang.org/x/crypto/ssh"
)

type HostSSHSession struct {
	SSHSession
}

func NewHostSSHSession(socket net.Conn, pf portForwardingManager, logger *log.Logger) *HostSSHSession {
	return &HostSSHSession{
		SSHSession: SSHSession{
			socket: socket,
			logger: logger},
	}
}

func (s *HostSSHSession) Host(ctx context.Context) error {
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
	go s.handleGlobalRequests(reqs)

	sshClient := ssh.NewClient(sshClientConn, chans, nil)
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

func (s *HostSSHSession) handleGlobalRequests(incoming <-chan *ssh.Request) {
	for r := range incoming {
		switch r.Type {
		case messages.PortForwardRequestType:
			s.handlePortForwardRequest(r)
		default:
			// This handles keepalive messages and matches
			// the behaviour of OpenSSH.
			r.Reply(false, nil)
		}
	}
}

func (s *HostSSHSession) handlePortForwardRequest(r *ssh.Request) {
	req := new(messages.PortForwardRequest)
	buf := bytes.NewReader(r.Payload)
	if err := req.Unmarshal(buf); err != nil {
		s.logger.Println(fmt.Sprintf("error unmarshalling port forward request: %s", err))
		r.Reply(false, nil)
		return
	}

	//s.pf.Add(int(req.Port()))
	reply := messages.NewPortForwardSuccess(req.Port())
	b, err := reply.Marshal()
	if err != nil {
		s.logger.Println(fmt.Sprintf("error marshaling port forward success response: %s", err))
		r.Reply(false, nil)
		return
	}

	r.Reply(true, b)
}
func (s *HostSSHSession) Read(p []byte) (n int, err error) {
	return s.reader.Read(p)
}

func (s *HostSSHSession) Write(p []byte) (n int, err error) {
	return s.writer.Write(p)
}

func (s *HostSSHSession) OpenChannel(ctx context.Context, channelType string, data []byte) (ssh.Channel, error) {
	channel, reqs, err := s.conn.OpenChannel(channelType, data)
	if err != nil {
		return nil, fmt.Errorf("failed to open channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	return channel, nil
}
