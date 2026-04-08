// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"encoding/binary"
	"io"
)

func readUint32(buf io.Reader) (i uint32, err error) {
	if err := binary.Read(buf, binary.BigEndian, &i); err != nil {
		return 0, err
	}
	return i, nil
}

func readBool(buf io.Reader) (bool, error) {
	var b [1]byte
	if _, err := io.ReadFull(buf, b[:]); err != nil {
		return false, err
	}
	return b[0] != 0, nil
}

func readString(buf io.Reader) (s string, err error) {
	var l uint32
	if l, err = readUint32(buf); err != nil {
		return "", err
	}
	if l > 0 {
		b := make([]byte, l)
		if _, err = io.ReadFull(buf, b); err != nil {
			return "", err
		}
		return string(b), nil
	}
	return "", nil
}
