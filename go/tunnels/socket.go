// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"crypto/tls"
	"fmt"
	"io"
	"net"
	"net/http"
	"time"

	"github.com/gorilla/websocket"
)

type socket struct {
	addr      string
	protocols []string
	headers   http.Header
	tlsConfig *tls.Config

	conn   *websocket.Conn
	reader io.Reader
}

func newSocket(uri string, protocols []string, headers http.Header, tlsConfig *tls.Config) *socket {
	return &socket{addr: uri, protocols: protocols, headers: headers, tlsConfig: tlsConfig}
}

func (s *socket) connect(ctx context.Context) error {
	dialer := websocket.Dialer{
		Proxy:            http.ProxyFromEnvironment,
		HandshakeTimeout: 45 * time.Second,
		TLSClientConfig:  s.tlsConfig,
		Subprotocols:     s.protocols,
	}
	ws, resp, err := dialer.Dial(s.addr, s.headers)
	if err != nil {
		if err == websocket.ErrBadHandshake {
			return fmt.Errorf("handshake failed with status %d", resp.StatusCode)
		}
		return err
	}
	s.conn = ws
	return nil
}

func (s *socket) Read(b []byte) (int, error) {
	if s.reader == nil {
		_, reader, err := s.conn.NextReader()
		if err != nil {
			return 0, err
		}

		s.reader = reader
	}

	bytesRead, err := s.reader.Read(b)
	if err != nil {
		s.reader = nil

		if err == io.EOF {
			err = nil
		}
	}

	return bytesRead, err
}

func (s *socket) Write(b []byte) (int, error) {
	nextWriter, err := s.conn.NextWriter(websocket.BinaryMessage)
	if err != nil {
		return 0, err
	}

	bytesWritten, err := nextWriter.Write(b)
	nextWriter.Close()

	return bytesWritten, err
}

func (s *socket) Close() error {
	return s.conn.Close()
}

func (s *socket) LocalAddr() net.Addr {
	return s.conn.LocalAddr()
}

func (s *socket) RemoteAddr() net.Addr {
	return s.conn.RemoteAddr()
}

func (s *socket) SetDeadline(t time.Time) error {
	if err := s.SetReadDeadline(t); err != nil {
		return err
	}

	return s.SetWriteDeadline(t)
}

func (s *socket) SetReadDeadline(t time.Time) error {
	return s.conn.SetReadDeadline(t)
}

func (s *socket) SetWriteDeadline(t time.Time) error {
	return s.conn.SetWriteDeadline(t)
}
