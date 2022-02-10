package messages

import (
	"bytes"
	"fmt"
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

func (c *channelOpen) marshalBinary() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := WriteUint32(buf, c.senderChannel); err != nil {
		return nil, fmt.Errorf("failed to write sender channel: %w", err)
	}
	if err := WriteUint32(buf, c.initialWindowSize); err != nil {
		return nil, fmt.Errorf("failed to write initial window size: %w", err)
	}
	if err := WriteUint32(buf, c.maximumPacketSize); err != nil {
		return nil, fmt.Errorf("failed to write maximum packet size: %w", err)
	}
	return buf.Bytes(), nil
}
