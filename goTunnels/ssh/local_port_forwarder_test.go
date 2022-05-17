// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package goTunnelssh

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"io"
	"net"
	"testing"
	"time"

	"github.com/microsoft/dev-tunnels/goTunnels/ssh/messages"
)

type mockChannelOpener struct {
	openChannelFunc func(string, string, int, string, int) (io.ReadWriteCloser, error)
}

func (m *mockChannelOpener) openChannel(
	channelType string,
	originIP string,
	originPort int,
	host string,
	port int,
) (io.ReadWriteCloser, error) {
	return m.openChannelFunc(channelType, originIP, originPort, host, port)
}

type mockChannel struct {
	*bytes.Buffer
}

func (m *mockChannel) Close() error {
	return nil
}

func TestLocalPortForwarderPortForwardChannelType(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	streamData := "stream-data"
	host := "127.0.0.1"
	port := 8080

	stream := &mockChannel{bytes.NewBufferString(streamData)}
	co := &mockChannelOpener{
		openChannelFunc: func(channelType, originIP string, originPort int, host string, port int) (io.ReadWriteCloser, error) {
			if channelType != messages.PortForwardChannelType {
				return nil, fmt.Errorf("expected channel type %s, got %s", messages.PortForwardChannelType, channelType)
			}
			return stream, nil
		},
	}

	lpf := newLocalPortForwarder(co, messages.PortForwardChannelType, host, port)
	done := make(chan error, 2)

	go func() {
		done <- lpf.startForwarding(ctx)
	}()

	go func() {
		var conn net.Conn

		// We retry DialTimeout in a loop to deal with a race in forwarder startup.
		for tries := 0; conn == nil && tries < 2; tries++ {
			conn, _ = net.DialTimeout("tcp", fmt.Sprintf(":%d", port), 2*time.Second)
			if conn == nil {
				time.Sleep(1 * time.Second)
			}
		}
		if conn == nil {
			done <- errors.New("failed to connect to forwarded port")
			return
		}

		b := make([]byte, len(streamData))
		if _, err := conn.Read(b); err != nil && err != io.EOF {
			done <- fmt.Errorf("reading stream: %w", err)
			return
		}
		if string(b) != streamData {
			done <- fmt.Errorf("stream data is not expected value, got: %s", string(b))
			return
		}

		if _, err := conn.Write([]byte("new-data")); err != nil {
			done <- fmt.Errorf("writing to stream: %w", err)
			return
		}

		done <- nil
	}()

	select {
	case err := <-done:
		if err != nil {
			t.Errorf("Unexpected error: %v", err)
		}
	}
}
