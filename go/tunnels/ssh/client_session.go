// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"log"
	"math"
	"net"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"github.com/microsoft/dev-tunnels/go/tunnels/ssh/messages"
	"golang.org/x/crypto/ssh"
)

type portForwardingManager interface {
	Add(port uint16)
}

type ClientSSHSession struct {
	*SSHSession
	pf              portForwardingManager
	listenersMu     sync.Mutex
	listeners       []net.Listener
	channels        uint32
	acceptLocalConn bool
	forwardedPorts  map[uint16]uint16
}

func NewClientSSHSession(socket net.Conn, pf portForwardingManager, acceptLocalConn bool, logger *log.Logger) *ClientSSHSession {
	return &ClientSSHSession{
		SSHSession: &SSHSession{
			socket: socket,
			logger: logger,
		},
		pf:              pf,
		acceptLocalConn: acceptLocalConn,
		listeners:       make([]net.Listener, 0),
		forwardedPorts:  make(map[uint16]uint16),
	}
}

func (s *ClientSSHSession) Connect(ctx context.Context) error {
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

func (s *ClientSSHSession) handleGlobalRequests(incoming <-chan *ssh.Request) {
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

func (s *ClientSSHSession) handlePortForwardRequest(r *ssh.Request) {
	req := new(messages.PortForwardRequest)
	buf := bytes.NewReader(r.Payload)
	if err := req.Unmarshal(buf); err != nil {
		s.logger.Printf(fmt.Sprintf("error unmarshalling port forward request: %s", err))
		r.Reply(false, nil)
		return
	}

	s.pf.Add(uint16(req.Port()))
	if s.acceptLocalConn {
		go s.forwardPort(context.Background(), uint16(req.Port()))
	}

	reply := messages.NewPortForwardSuccess(req.Port())
	b, err := reply.Marshal()
	if err != nil {
		s.logger.Printf(fmt.Sprintf("error marshaling port forward success response: %s", err))
		r.Reply(false, nil)
		return
	}

	r.Reply(true, b)
}

func (s *ClientSSHSession) OpenChannel(ctx context.Context, channelType string, data []byte) (ssh.Channel, error) {
	channel, reqs, err := s.conn.OpenChannel(channelType, data)
	if err != nil {
		return nil, fmt.Errorf("failed to open channel: %w", err)
	}
	go ssh.DiscardRequests(reqs)

	return channel, nil
}

func (s *ClientSSHSession) forwardPort(ctx context.Context, port uint16) error {
	var listener net.Listener

	var i uint16 = 0
	for i < 10 {
		portNum := port + i
		innerListener, err := net.Listen("tcp", fmt.Sprintf(":%d", portNum))
		if err == nil {
			listener = innerListener
			break
		}
		i++
	}
	if listener == nil {
		innerListener, err := net.Listen("tcp", ":0")
		if err != nil {
			return fmt.Errorf("error creating listener: %w", err)
		}
		listener = innerListener
	}
	addressSlice := strings.Split(listener.Addr().String(), ":")
	portNum, err := strconv.ParseUint(addressSlice[len(addressSlice)-1], 10, 16)
	if err != nil {
		return fmt.Errorf("error getting port number: %w", err)
	}
	if portNum > 0 && portNum <= math.MaxUint16 {
		s.forwardedPorts[port] = uint16(portNum)
	} else {
		return fmt.Errorf("port number %d is out of bounds", portNum)
	}

	errc := make(chan error, 1)
	sendError := func(err error) {
		// Use non-blocking send, to avoid goroutines getting
		// stuck in case of concurrent or sequential errors.
		select {
		case errc <- err:
		default:
		}
	}
	fmt.Printf("Client connected at %v to host port %v\n", listener.Addr(), port)

	go func() {
		for {
			conn, err := listener.Accept()
			if err != nil {
				sendError(err)
				return
			}
			s.listenersMu.Lock()
			s.listeners = append(s.listeners, listener)
			s.listenersMu.Unlock()

			go func() {
				if err := s.handleConnection(ctx, conn, port); err != nil {
					sendError(err)
				}
			}()
		}
	}()

	return awaitError(ctx, errc)
}

func (s *ClientSSHSession) handleConnection(ctx context.Context, conn io.ReadWriteCloser, port uint16) (err error) {
	defer safeClose(conn, &err)

	channel, err := s.openStreamingChannel(ctx, port)
	if err != nil {
		return fmt.Errorf("failed to open streaming channel: %w", err)
	}

	// Ideally we would call safeClose again, but (*ssh.channel).Close
	// appears to have a bug that causes it return io.EOF spuriously
	// if its peer closed first; see github.com/golang/go/issues/38115.
	defer func() {
		closeErr := channel.Close()
		if err == nil && closeErr != io.EOF {
			err = closeErr
		}
	}()

	errs := make(chan error, 2)
	copyConn := func(w io.Writer, r io.Reader) {
		_, err := io.Copy(w, r)
		errs <- err
	}

	go copyConn(conn, channel)
	go copyConn(channel, conn)

	// Wait until context is cancelled or both copies are done.
	// Discard errors from io.Copy; they should not cause (e.g.) failures.
	for i := 0; ; {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-errs:
			i++
			if i == 2 {
				return nil
			}
		}
	}
}

func (s *ClientSSHSession) NextChannelID() uint32 {
	return atomic.AddUint32(&s.channels, 1)
}

func (s *ClientSSHSession) openStreamingChannel(ctx context.Context, port uint16) (ssh.Channel, error) {
	portForwardChannel := messages.NewPortForwardChannel(
		s.NextChannelID(),
		"127.0.0.1",
		uint32(port),
		"",
		0,
	)
	data, err := portForwardChannel.Marshal()
	if err != nil {
		return nil, fmt.Errorf("failed to marshal port forward channel open message: %w", err)
	}

	channel, err := s.OpenChannel(ctx, portForwardChannel.Type(), data)
	if err != nil {
		return nil, fmt.Errorf("failed to open port forward channel: %w", err)
	}

	return channel, nil
}

func (s *ClientSSHSession) Close() error {
	if s.Session != nil {
		s.Session.Close()
	}
	if s.conn != nil {
		s.conn.Close()
	}
	if s.socket != nil {
		s.socket.Close()
	}
	for _, listener := range s.listeners {
		listener.Close()
	}
	return nil
}
