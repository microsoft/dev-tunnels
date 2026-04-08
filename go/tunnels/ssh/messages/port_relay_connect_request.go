// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"fmt"
	"io"
)

// PortRelayConnectRequest is the V2 forwarded-tcpip channel extra data sent
// by the relay when opening a channel to the host. It extends the standard
// forwarded-tcpip fields with AccessToken and IsE2EEncryptionRequested.
//
// Wire format:
// | host (string) | port (uint32) | originatorIP (string) | originatorPort (uint32) |
// | accessToken (string) | isE2EEncryptionRequested (bool) |
type PortRelayConnectRequest struct {
	Host                     string
	Port                     uint32
	OriginatorIP             string
	OriginatorPort           uint32
	AccessToken              string
	IsE2EEncryptionRequested bool
}

// Marshal serializes the request to wire format.
func (r *PortRelayConnectRequest) Marshal() ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := writeString(buf, r.Host); err != nil {
		return nil, fmt.Errorf("error writing host: %w", err)
	}
	if err := writeUint32(buf, r.Port); err != nil {
		return nil, fmt.Errorf("error writing port: %w", err)
	}
	if err := writeString(buf, r.OriginatorIP); err != nil {
		return nil, fmt.Errorf("error writing originator IP: %w", err)
	}
	if err := writeUint32(buf, r.OriginatorPort); err != nil {
		return nil, fmt.Errorf("error writing originator port: %w", err)
	}
	if err := writeString(buf, r.AccessToken); err != nil {
		return nil, fmt.Errorf("error writing access token: %w", err)
	}
	if err := writeBool(buf, r.IsE2EEncryptionRequested); err != nil {
		return nil, fmt.Errorf("error writing isE2EEncryptionRequested: %w", err)
	}
	return buf.Bytes(), nil
}

// Unmarshal deserializes the request from wire format.
func (r *PortRelayConnectRequest) Unmarshal(buf io.Reader) error {
	host, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading host: %w", err)
	}
	port, err := readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading port: %w", err)
	}
	originatorIP, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading originator IP: %w", err)
	}
	originatorPort, err := readUint32(buf)
	if err != nil {
		return fmt.Errorf("error reading originator port: %w", err)
	}
	accessToken, err := readString(buf)
	if err != nil {
		return fmt.Errorf("error reading access token: %w", err)
	}
	isE2EEncryptionRequested, err := readBool(buf)
	if err != nil {
		return fmt.Errorf("error reading isE2EEncryptionRequested: %w", err)
	}
	r.Host = host
	r.Port = port
	r.OriginatorIP = originatorIP
	r.OriginatorPort = originatorPort
	r.AccessToken = accessToken
	r.IsE2EEncryptionRequested = isE2EEncryptionRequested
	return nil
}
