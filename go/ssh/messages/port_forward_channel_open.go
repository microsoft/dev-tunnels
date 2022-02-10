package messages

import (
	"bytes"
	"fmt"
)

type PortForwardChannelOpen struct {
	channelOpen         *channelOpen
	host                string
	port                uint32
	originatorIPAddress string
	originatorPort      uint32
}

func NewPortForwardChannelOpen(senderChannel uint32, host string, port uint32, originatorIPAddress string, originatorPort uint32) *PortForwardChannelOpen {
	co := newChannelOpen(senderChannel, 0, 0)

	return &PortForwardChannelOpen{
		channelOpen:         co,
		host:                host,
		port:                port,
		originatorIPAddress: originatorIPAddress,
		originatorPort:      originatorPort,
	}
}

func (pfc *PortForwardChannelOpen) Type() string {
	return "forwarded-tcpip"
}

func (pfc *PortForwardChannelOpen) MarshalBinary() ([]byte, error) {
	b, err := pfc.channelOpen.marshalBinary()
	if err != nil {
		return nil, fmt.Errorf("error marshaling channel open: %w", err)
	}

	buf := bytes.NewBuffer(b)
	if err := WriteString(buf, pfc.host); err != nil {
		return nil, fmt.Errorf("error writing host: %w", err)
	}
	if err := WriteUint32(buf, pfc.port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	if err := WriteString(buf, pfc.originatorIPAddress); err != nil {
		return nil, fmt.Errorf("error writing originator ip address: %w", err)
	}
	if err := WriteUint32(buf, pfc.originatorPort); err != nil {
		return nil, fmt.Errorf("error writing originator port: %w", err)
	}

	return buf.Bytes(), nil
}
