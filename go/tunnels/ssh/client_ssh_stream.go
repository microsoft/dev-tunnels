// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import (
	"net"
	"sync"
	"time"

	"golang.org/x/crypto/ssh"
)

// dummyAddr is a placeholder net.Addr used by channelConn.
type dummyAddr struct{}

func (dummyAddr) Network() string { return "tunnel" }
func (dummyAddr) String() string  { return "tunnel" }

// channelConn wraps an ssh.Channel to implement the net.Conn interface.
// This is needed to pass an SSH channel to ssh.NewServerConn for
// per-client SSH sessions inside the relay tunnel.
type channelConn struct {
	ssh.Channel

	mu    sync.Mutex
	timer *time.Timer
}

// LocalAddr returns a dummy address.
func (c *channelConn) LocalAddr() net.Addr {
	return dummyAddr{}
}

// RemoteAddr returns a dummy address.
func (c *channelConn) RemoteAddr() net.Addr {
	return dummyAddr{}
}

// SetDeadline sets a deadline that closes the channel on expiration.
// This is required for SSH handshake timeouts. A zero-value time clears
// the timer without closing the channel.
func (c *channelConn) SetDeadline(t time.Time) error {
	c.mu.Lock()
	defer c.mu.Unlock()

	// Stop any existing timer to prevent leaks.
	if c.timer != nil {
		c.timer.Stop()
		c.timer = nil
	}

	// A zero-value deadline clears the timer.
	if t.IsZero() {
		return nil
	}

	d := time.Until(t)
	if d <= 0 {
		// Deadline already passed, close immediately.
		c.Channel.Close()
		return nil
	}

	c.timer = time.AfterFunc(d, func() {
		c.Channel.Close()
	})
	return nil
}

// SetReadDeadline delegates to SetDeadline.
func (c *channelConn) SetReadDeadline(t time.Time) error {
	return c.SetDeadline(t)
}

// SetWriteDeadline is a no-op returning nil.
func (c *channelConn) SetWriteDeadline(t time.Time) error {
	return nil
}
