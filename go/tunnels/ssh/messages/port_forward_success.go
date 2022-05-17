// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"io"
)

type PortForwardSuccess struct {
	port uint32
}

func NewPortForwardSuccess(port uint32) *PortForwardSuccess {
	return &PortForwardSuccess{
		port: port,
	}
}

func (pfs *PortForwardSuccess) Port() uint32 {
	return pfs.port
}

func (pfs *PortForwardSuccess) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeUint32(buf, pfs.port); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}

func (pfs *PortForwardSuccess) Unmarshal(buf io.Reader) error {
	port, err := readUint32(buf)
	if err != nil {
		return err
	}
	pfs.port = port
	return nil
}
