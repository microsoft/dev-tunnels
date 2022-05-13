// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"context"
	"errors"
	"testing"

	"golang.org/x/crypto/ssh"
)

type mockActivator struct {
	ActivateFunc func(context.Context, *Session) error
}

func (m *mockActivator) Activate(ctx context.Context, s *Session) error {
	return m.ActivateFunc(ctx, s)
}

func TestSessionActivate(t *testing.T) {
	session := NewSession(nil)
	ma := &mockActivator{
		ActivateFunc: func(ctx context.Context, s *Session) error {
			if s != session {
				return errors.New("invalid session")
			}
			return nil
		},
	}
	if err := session.Activate(context.Background(), ma); err != nil {
		t.Errorf("session.Activate() error = %v", err)
	}
}

type mockNewChannel struct {
	AcceptFunc      func() (ssh.Channel, <-chan *ssh.Request, error)
	ChannelTypeFunc func() string
	RejectFunc      func(ssh.RejectionReason, string) error
	ExtraDataFunc   func() []byte
}

func (m *mockNewChannel) Accept() (ssh.Channel, <-chan *ssh.Request, error) {
	return m.AcceptFunc()
}

func (m *mockNewChannel) ExtraData() []byte {
	return m.ExtraDataFunc()
}

func (m *mockNewChannel) ChannelType() string {
	return m.ChannelTypeFunc()
}

func (m *mockNewChannel) Reject(reason ssh.RejectionReason, message string) error {
	return m.RejectFunc(reason, message)
}

func TestSessionChannels(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	channelType := "testChannel"
	session := NewSession(nil)
	var n int
	session.AddChannelHandler(channelType, func(ctx context.Context, newChannel ssh.NewChannel) {
		n++
	})

	chans := make(chan ssh.NewChannel)
	go session.handleChannels(ctx, chans)

	// successful channel
	chans <- &mockNewChannel{
		ChannelTypeFunc: func() string {
			return channelType
		},
	}

	// rejected channel
	called := make(chan struct{})
	chans <- &mockNewChannel{
		ChannelTypeFunc: func() string {
			return "otherChannel"
		},
		RejectFunc: func(reason ssh.RejectionReason, message string) error {
			close(called)
			return nil
		},
	}

	if n != 1 {
		t.Errorf("n = %d, want 1", n)
	}

	// wait for the channel to be rejected
	<-called
}

type mockSSHRequest struct {
	TypeFunc  func() string
	ReplyFunc func(bool, []byte) error
}

func (m *mockSSHRequest) Type() string {
	return m.TypeFunc()
}

func (m *mockSSHRequest) Reply(ok bool, message []byte) error {
	return m.ReplyFunc(ok, message)
}

func TestSessionRequests(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	session := NewSession(nil)
	var n int
	session.AddRequestHandler("testRequest", func(ctx context.Context, req SSHRequest) {
		n++
	})

	reqs := make(chan SSHRequest)
	go session.handleRequests(ctx, reqs)

	reqs <- &mockSSHRequest{
		TypeFunc: func() string {
			return "testRequest"
		},
	}
	if n != 1 {
		t.Errorf("n = %d, want 1", n)
	}

	called := make(chan struct{})
	reqs <- &mockSSHRequest{
		TypeFunc: func() string {
			return "otherRequest"
		},
		ReplyFunc: func(ok bool, message []byte) error {
			close(called)
			return nil
		},
	}

	// wait for the request to be rejected
	<-called
}
