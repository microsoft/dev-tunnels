// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

const (
	PortForwardCancelRequestType = "cancel-tcpip-forward"
)

type PortForwardCancelRequest struct {
	addressToBind string
	port          uint32
}

func NewPortForwardCancelRequest(addressToBind string, port uint32) *PortForwardCancelRequest {
	return &PortForwardCancelRequest{
		addressToBind: addressToBind,
		port:          port,
	}
}

func (pfcr *PortForwardCancelRequest) Port() uint32 {
	return pfcr.port
}

func (pfcr *PortForwardCancelRequest) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeString(buf, pfcr.addressToBind); err != nil {
		return nil, fmt.Errorf("error writing address to bind: %w", err)
	}
	if err := writeUint32(buf, pfcr.port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	return buf.Bytes(), nil
}

func (pfcr *PortForwardCancelRequest) Unmarshal(buf io.Reader) error {
	addressToBind, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading address to bind: %w", err)
	}
	port, err := readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading port: %w", err)
	}
	pfcr.addressToBind = addressToBind
	pfcr.port = port
	return nil
}
