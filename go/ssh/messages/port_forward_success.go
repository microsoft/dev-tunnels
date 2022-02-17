package messages

import "bytes"

type PortForwardSuccess struct {
	port uint32
}

func NewPortForwardSuccess(port uint32) *PortForwardSuccess {
	return &PortForwardSuccess{
		port: port,
	}
}

func (pfs *PortForwardSuccess) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeUint32(buf, pfs.port); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}
