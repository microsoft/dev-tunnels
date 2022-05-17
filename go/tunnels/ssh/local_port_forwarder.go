// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"context"
	"fmt"
	"io"
	"net"
	"os"
	"syscall"
)

type channelOpener interface {
	openChannel(channelType, originIP string, originPort int, host string, port int) (io.ReadWriteCloser, error)
}

type localPortForwarder struct {
	co          channelOpener
	channelType string
	localIP     string
	localPort   int
}

func newLocalPortForwarder(co channelOpener, channelType string, localIP string, localPort int) *localPortForwarder {
	return &localPortForwarder{co, channelType, localIP, localPort}
}

func (l *localPortForwarder) startForwarding(ctx context.Context) (err error) {
	listenAddress := l.localIP
	// TODO(josebalius): check for remote version of ssh
	// and look to implement a wrapper around listener that supports changing the port
	// probably best to double check that we actually need this?

	listener, err := net.Listen("tcp", fmt.Sprintf("%s:%d", listenAddress, l.localPort))
	if err != nil {
		return fmt.Errorf("failed to listen on local port %d: %v", l.localPort, err)
	}
	defer safeClose(listener, &err)

	// The SSH protocol specifies that "localhost" or "" (any) should be dual-mode (IPv4 and IPv6).
	// So 2 TCP listener instances are required in those cases.
	var listener2 net.Listener
	if l.localIP == "127.0.0.1" || l.localIP == "0.0.0.0" {
		// Call the factory again to create another listener, but this time with the
		// corresponding IPv6 local address
		if listenAddress == "0.0.0.0" {
			listenAddress = "::"
		} else {
			listenAddress = "::1"
		}

		listener2, err = net.Listen("tcp", fmt.Sprintf("[%s]:%d", listenAddress, l.localPort))
		if err != nil {
			// If the OS doesn't support IPv6, we are okay with the error, otherwise return
			if sys, ok := err.(*os.SyscallError); !ok || sys.Err != syscall.EADDRNOTAVAIL {
				return fmt.Errorf("failed to listen twice on local port %d: %v", l.localPort, err)
			}
		}
		defer safeClose(listener2, &err)
	}

	errc := make(chan error, 1)
	go func() {
		err := l.acceptConnections(ctx, listener)
		if err != nil {
			sendError(errc, err)
		}
	}()

	if listener2 != nil {
		go func() {
			err := l.acceptConnections(ctx, listener2)
			if err != nil {
				sendError(errc, err)
			}
		}()
	}

	return awaitError(ctx, errc)
}

func (l *localPortForwarder) acceptConnections(ctx context.Context, listener net.Listener) error {
	errc := make(chan error, 1)
	go func() {
		for {
			conn, err := listener.Accept()
			if err != nil {
				sendError(errc, err)
				return
			}

			go func() {
				err := l.handleConnection(ctx, conn)
				if err != nil {
					sendError(errc, err)
				}
			}()
		}
	}()
	return awaitError(ctx, errc)
}

func (l *localPortForwarder) handleConnection(ctx context.Context, conn net.Conn) (err error) {
	defer safeClose(conn, &err)

	channel, err := l.co.openChannel(
		l.channelType, conn.RemoteAddr().String(), conn.RemoteAddr().(*net.TCPAddr).Port, l.localIP, l.localPort,
	)
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

func safeClose(c io.Closer, err *error) {
	if closerErr := c.Close(); *err == nil {
		*err = closerErr
	}
}

func sendError(errc chan<- error, err error) {
	select {
	case errc <- err:
	default:
	}
}

func awaitError(ctx context.Context, errc chan error) error {
	select {
	case err := <-errc:
		return err
	case <-ctx.Done():
		return ctx.Err()
	}
}
