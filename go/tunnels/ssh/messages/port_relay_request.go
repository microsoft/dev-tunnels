// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

// PortRelayRequest is the V2 tcpip-forward request payload sent by the host
// to the relay. It extends the standard tcpip-forward with an AccessToken field.
//
// Wire format: | addressToBind (string) | port (uint32) | accessToken (string) |
type PortRelayRequest struct {
	addressToBind string
	port          uint32
	accessToken   string
}

// NewPortRelayRequest creates a new PortRelayRequest.
func NewPortRelayRequest(addressToBind string, port uint32, accessToken string) *PortRelayRequest {
	return &PortRelayRequest{
		addressToBind: addressToBind,
		port:          port,
		accessToken:   accessToken,
	}
}

// Port returns the port number.
func (r *PortRelayRequest) Port() uint32 {
	return r.port
}

// AccessToken returns the access token.
func (r *PortRelayRequest) AccessToken() string {
	return r.accessToken
}

// Marshal serializes the request to wire format.
func (r *PortRelayRequest) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeString(buf, r.addressToBind); err != nil {
		return nil, fmt.Errorf("error writing address to bind: %w", err)
	}
	if err := writeUint32(buf, r.port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	if err := writeString(buf, r.accessToken); err != nil {
		return nil, fmt.Errorf("error writing access token: %w", err)
	}
	return buf.Bytes(), nil
}

// Unmarshal deserializes the request from wire format.
func (r *PortRelayRequest) Unmarshal(buf io.Reader) error {
	addressToBind, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading address to bind: %w", err)
	}
	port, err := readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading port: %w", err)
	}
	accessToken, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading access token: %w", err)
	}
	r.addressToBind = addressToBind
	r.port = port
	r.accessToken = accessToken
	return nil
}
