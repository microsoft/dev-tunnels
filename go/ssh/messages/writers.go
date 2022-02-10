package messages

import (
	"bytes"
	"encoding/binary"
	"fmt"
)

func WriteString(buf *bytes.Buffer, s string) error {
	return WriteBinary(buf, []byte(s))
}

func WriteBinary(buf *bytes.Buffer, p []byte) error {
	if err := WriteUint32(buf, uint32(len(p))); err != nil {
		return fmt.Errorf("failed to write length of binary data: %w", err)
	}
	if _, err := buf.Write(p); err != nil {
		return fmt.Errorf("failed to write binary data: %w", err)
	}
	return nil
}

func WriteUint32(buf *bytes.Buffer, v uint32) error {
	return binary.Write(buf, binary.BigEndian, v)
}
