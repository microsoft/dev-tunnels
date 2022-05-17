// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

type channelOpen struct {
	senderChannel     uint32
	initialWindowSize uint32
	maximumPacketSize uint32
}

const (
	defaultInitialWindowSize = 1024 * 1024
	defaultMaximumPacketSize = 16 * 1024
)

func newChannelOpen(senderChannel uint32, initialWindowSize uint32, maximumPacketSize uint32) *channelOpen {
	if initialWindowSize == 0 {
		initialWindowSize = defaultInitialWindowSize
	}
	if maximumPacketSize == 0 {
		maximumPacketSize = defaultMaximumPacketSize
	}
	return &channelOpen{
		senderChannel:     senderChannel,
		initialWindowSize: initialWindowSize,
		maximumPacketSize: maximumPacketSize,
	}
}

func (c *channelOpen) marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeUint32(buf, c.senderChannel); err != nil {
		return nil, fmt.Errorf("failed to write sender channel: %w", err)
	}
	if err := writeUint32(buf, c.initialWindowSize); err != nil {
		return nil, fmt.Errorf("failed to write initial window size: %w", err)
	}
	if err := writeUint32(buf, c.maximumPacketSize); err != nil {
		return nil, fmt.Errorf("failed to write maximum packet size: %w", err)
	}
	return buf.Bytes(), nil
}

func (c *channelOpen) unmarshal(buf io.Reader) (err error) {
	c.senderChannel, err = readUint32(buf)
	if err != nil {
		return fmt.Errorf("failed to read sender channel: %w", err)
	}
	c.initialWindowSize, err = readUint32(buf)
	if err != nil {
		return fmt.Errorf("failed to read initial window size: %w", err)
	}
	c.maximumPacketSize, err = readUint32(buf)
	if err != nil {
		return fmt.Errorf("failed to read maximum packet size: %w", err)
	}
	return nil
}
