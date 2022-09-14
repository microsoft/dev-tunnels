// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

const SessionRequestMessageType = 80

type SessionRequestMessage struct {
	requestType string
	wantReply   bool
	messageType byte
}

func NewSessionRequestMessage(requestType string, wantReply bool) *SessionRequestMessage {
	return &SessionRequestMessage{
		requestType: requestType,
		wantReply:   wantReply,
		messageType: SessionRequestMessageType,
	}
}

func (srm *SessionRequestMessage) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := buf.WriteByte(srm.messageType); err != nil {
		return nil, fmt.Errorf("error writing messageType: %w", err)
	}
	if err := writeString(buf, srm.requestType); err != nil {
		return nil, fmt.Errorf("error writing requestType: %w", err)
	}
	if err := writeBool(buf, srm.wantReply); err != nil {
		return nil, fmt.Errorf("error writing wantReply: %w", err)
	}
	return buf.Bytes(), nil
}

func (srm *SessionRequestMessage) Unmarshal(buf io.Reader) error {
	requestType, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading requestType: %w", err)
	}
	wantReply, err := readBool(buf)
	if err != nil {
		return fmt.Errorf("error reading wantReply: %w", err)
	}
	messageType := make([]byte, 1)
	bytesRead, err := buf.Read(messageType)
	if err != nil || bytesRead != 1 {
		return fmt.Errorf("error reading wantReply: %w", err)
	}
	srm.requestType = requestType
	srm.wantReply = wantReply
	srm.messageType = messageType[0]
	return nil
}
