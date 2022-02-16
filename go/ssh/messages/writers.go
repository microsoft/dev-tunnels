package messages

import (
	"bytes"
	"encoding/binary"
	"fmt"
)

func writeString(buf *bytes.Buffer, s string) error {
	return writeBinary(buf, []byte(s))
}

func writeBinary(buf *bytes.Buffer, p []byte) error {
	if err := writeUint32(buf, uint32(len(p))); err != nil {
		return fmt.Errorf("failed to write length of binary data: %w", err)
	}
	if _, err := buf.Write(p); err != nil {
		return fmt.Errorf("failed to write binary data: %w", err)
	}
	return nil
}

func writeUint32(buf *bytes.Buffer, v uint32) error {
	return binary.Write(buf, binary.BigEndian, v)
}
