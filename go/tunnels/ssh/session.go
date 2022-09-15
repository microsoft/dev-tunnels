// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"context"
	"fmt"
	"io"
	"log"
	"net"
	"sync"
	"time"

	"golang.org/x/crypto/ssh"
)

type channelHandlerFunc func(ctx context.Context, channel ssh.NewChannel)
type requestHandlerFunc func(ctx context.Context, req SSHRequest)

// Session is a wrapper around an SSH session designed for communicating
// with a remote tunnels SSH server. It supports the activation of services
// via the activator interface.
type Session struct {
	*ssh.Session

	socket net.Conn
	conn   ssh.Conn

	channelHandlersMu sync.RWMutex
	channelHandlers   map[string]channelHandlerFunc

	requestHandlersMu sync.RWMutex
	requestHandlers   map[string]requestHandlerFunc
}

// NewSession creates a new session.
func NewSession(socket net.Conn) *Session {
	return &Session{socket: socket}
}

// Connect connects to the remote tunnel SSH server.
func (s *Session) Connect(ctx context.Context) (err error) {
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

	conn, chans, reqs, err := ssh.NewClientConn(s.socket, "", &clientConfig)
	if err != nil {
		return fmt.Errorf("error creating SSH client connection: %w", err)
	}
	s.conn = conn

	go s.handleChannels(ctx, chans)
	go s.handleRequests(ctx, s.convertRequests(reqs))

	sshClient := ssh.NewClient(s.conn, nil, nil)
	s.Session, err = sshClient.NewSession()
	if err != nil {
		return fmt.Errorf("error creating ssh client session: %w", err)
	}

	return nil
}

type activator interface {
	Activate(ctx context.Context, session *Session) error
}

// Active calls the Activate method on the activator interface and passes
// the session to it.
func (s *Session) Activate(ctx context.Context, a activator) error {
	return a.Activate(ctx, s)
}

// AddChannelHandler adds a handler for a channel type.
func (s *Session) AddChannelHandler(channelType string, handler channelHandlerFunc) {
	s.channelHandlersMu.Lock()
	defer s.channelHandlersMu.Unlock()

	if s.channelHandlers == nil {
		s.channelHandlers = make(map[string]channelHandlerFunc)
	}

	s.channelHandlers[channelType] = handler
}

// AddRequestHandler adds a handler for a request type.
func (s *Session) AddRequestHandler(requestType string, handler requestHandlerFunc) {
	s.requestHandlersMu.Lock()
	defer s.requestHandlersMu.Unlock()

	if s.requestHandlers == nil {
		s.requestHandlers = make(map[string]requestHandlerFunc)
	}

	s.requestHandlers[requestType] = handler
}

func (s *Session) handleChannels(ctx context.Context, chans <-chan ssh.NewChannel) {
	for {
		select {
		case <-ctx.Done():
			return
		case newChannel := <-chans:
			s.channelHandlersMu.RLock()
			handler, ok := s.channelHandlers[newChannel.ChannelType()]
			s.channelHandlersMu.RUnlock()

			if !ok {
				newChannel.Reject(ssh.UnknownChannelType, "unknown channel type")
				continue
			}

			handler(ctx, newChannel)
		}
	}
}

func (s *Session) handleRequests(ctx context.Context, reqs <-chan SSHRequest) {
	for {
		select {
		case <-ctx.Done():
			return
		case req, ok := <-reqs:
			s.requestHandlersMu.RLock()
			handler, ok := s.requestHandlers[req.Type()]
			s.requestHandlersMu.RUnlock()

			if !ok {
				// Preserve OpenSSH behavior: if the request type is unknown,
				// reject it.
				req.Reply(false, nil)
				continue
			}

			handler(ctx, req)
		}
	}
}

// TODO(josebalius): Deprecate SSHSession struct.
type SSHSession struct {
	*ssh.Session
	socket net.Conn
	conn   ssh.Conn
	reader io.Reader
	writer io.Writer
	logger *log.Logger
}

func (s *SSHSession) Read(p []byte) (n int, err error) {
	return s.reader.Read(p)
}

func (s *SSHSession) Write(p []byte) (n int, err error) {
	return s.writer.Write(p)
}

func (s *SSHSession) SendSessionRequest(name string, wantReply bool, payload []byte) (bool, []byte, error) {
	return s.conn.SendRequest(name, wantReply, payload)
}
