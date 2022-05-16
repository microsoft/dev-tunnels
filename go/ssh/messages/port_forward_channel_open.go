// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

const PortForwardChannelType = "forwarded-tcpip"

type PortForwardChannel struct {
	channelOpen         *channelOpen
	host                string
	port                uint32
	originatorIPAddress string
	originatorPort      uint32
}

func NewPortForwardChannel(senderChannel uint32, host string, port uint32, originatorIPAddress string, originatorPort uint32) *PortForwardChannel {
	co := newChannelOpen(senderChannel, 0, 0)

	return &PortForwardChannel{
		channelOpen:         co,
		host:                host,
		port:                port,
		originatorIPAddress: originatorIPAddress,
		originatorPort:      originatorPort,
	}
}

func (pfc *PortForwardChannel) Type() string {
	return PortForwardChannelType
}

func (pfc *PortForwardChannel) Port() uint32 {
	return pfc.port
}

// Marshal returns the byte representation of the PortForwardChannel.
// This does not include the channelOpen as it is already included in the ssh message.
func (pfc *PortForwardChannel) Marshal() ([]byte, error) {
	var buff []byte
	buf := bytes.NewBuffer(buff)
	if err := writeString(buf, pfc.host); err != nil {
		return nil, fmt.Errorf("error writing host: %w", err)
	}
	if err := writeUint32(buf, pfc.port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	if err := writeString(buf, pfc.originatorIPAddress); err != nil {
		return nil, fmt.Errorf("error writing originator ip address: %w", err)
	}
	if err := writeUint32(buf, pfc.originatorPort); err != nil {
		return nil, fmt.Errorf("error writing originator port: %w", err)
	}

	return buf.Bytes(), nil
}

// Unmarshal parses the byte representation of the PortForwardChannel.
// This does not include the channelOpen.
func (pfc *PortForwardChannel) Unmarshal(buf io.Reader) (err error) {
	pfc.host, err = readString(buf)
	if err != nil {
		return fmt.Errorf("error reading host: %w", err)
	}

	pfc.port, err = readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading port: %w", err)
	}

	pfc.originatorIPAddress, err = readString(buf)
	if err != nil {
		return fmt.Errorf("error reading originator ip address: %w", err)
	}

	pfc.originatorPort, err = readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading originator port: %w", err)
	}

	return nil
}
