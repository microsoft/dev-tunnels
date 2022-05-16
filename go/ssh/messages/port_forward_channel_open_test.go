// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package messages

import (
	"bytes"
	"testing"
)

func TestUnmarshalPortForwardChannel(t *testing.T) {
	pfc := NewPortForwardChannel(11, "127.0.0.1", 8001, "999", 8002)
	b, err := pfc.Marshal()
	if err != nil {
		t.Error(err)
	}

	buf := bytes.NewReader(b)
	pfc2 := &PortForwardChannel{}
	if err := pfc2.Unmarshal(buf); err != nil {
		t.Error(err)
	}

	if pfc2.host != pfc.host {
		t.Errorf("host: expected %v, got %v", pfc.host, pfc2.host)
	}

	if pfc2.port != pfc.port {
		t.Errorf("port: expected %v, got %v", pfc.port, pfc2.port)
	}

	if pfc2.originatorIPAddress != pfc.originatorIPAddress {
		t.Errorf("originHost: expected %v, got %v", pfc.originatorIPAddress, pfc2.originatorIPAddress)
	}

	if pfc2.originatorPort != pfc.originatorPort {
		t.Errorf("originPort: expected %v, got %v", pfc.originatorPort, pfc2.originatorPort)
	}
}
