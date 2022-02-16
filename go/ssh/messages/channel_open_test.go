package messages

import (
	"bytes"
	"testing"
)

func TestUnmarshalChannelOpen(t *testing.T) {
	co := newChannelOpen(11, 0, 0)
	b, err := co.marshalBinary()
	if err != nil {
		t.Error(err)
	}

	buf := bytes.NewReader(b)
	co2 := new(channelOpen)
	if err := co2.unmarshalBinary(buf); err != nil {
		t.Error(err)
	}

	if co.senderChannel != co2.senderChannel {
		t.Errorf("senderChannel: want %d, got %d", co.senderChannel, co2.senderChannel)
	}

	if co.initialWindowSize != co2.initialWindowSize {
		t.Errorf("initialWindowSize: want %d, got %d", co.initialWindowSize, co2.initialWindowSize)
	}

	if co.maximumPacketSize != co2.maximumPacketSize {
		t.Errorf("maximumPacketSize: want %d, got %d", co.maximumPacketSize, co2.maximumPacketSize)
	}
}
