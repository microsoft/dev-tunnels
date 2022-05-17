// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

const (
	PortForwardRequestType = "tcpip-forward"
)

type PortForwardRequest struct {
	addressToBind string
	port          uint32
}

func NewPortForwardRequest(addressToBind string, port uint32) *PortForwardRequest {
	return &PortForwardRequest{
		addressToBind: addressToBind,
		port:          port,
	}
}

func (pfr *PortForwardRequest) Port() uint32 {
	return pfr.port
}

func (pfr *PortForwardRequest) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeString(buf, pfr.addressToBind); err != nil {
		return nil, fmt.Errorf("error writing address to bind: %w", err)
	}
	if err := writeUint32(buf, pfr.port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	return buf.Bytes(), nil
}

func (pfr *PortForwardRequest) Unmarshal(buf io.Reader) error {
	addressToBind, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading address to bind: %w", err)
	}
	port, err := readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading port: %w", err)
	}
	pfr.addressToBind = addressToBind
	pfr.port = port
	return nil
}
