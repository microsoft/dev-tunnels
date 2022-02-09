package tunnels

import (
	"context"
	"errors"
	"fmt"
	"log"
	"os"
	"strings"
	"testing"

	tunnelstest "github.com/microsoft/tunnels/go/test"
)

func TestSuccessfulConnect(t *testing.T) {
	relayServer, err := tunnelstest.NewRelayServer()
	if err != nil {
		t.Fatal(err)
	}

	hostURL := strings.Replace(relayServer.URL(), "http://", "ws://", 1)
	tunnel := Tunnel{
		Endpoints: []*TunnelEndpoint{
			{
				HostID:         "host1",
				ClientRelayURI: hostURL,
			},
		},
	}

	logger := log.New(os.Stdout, "", log.LstdFlags)
	done := make(chan error)
	go func() {
		c, err := Connect(context.Background(), logger, &tunnel, "")
		if err != nil {
			done <- fmt.Errorf("connect failed: %v", err)
			return
		}
		if c == nil {
			done <- errors.New("nil connection")
			return
		}
		done <- nil
	}()

	select {
	case err := <-relayServer.Err():
		t.Errorf("relay server error: %v", err)
	case err := <-done:
		if err != nil {
			t.Errorf(err.Error())
		}
	}
}

func TestReturnsErrWhenTunnelIsNil(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)
	_, err := Connect(context.Background(), logger, nil, "")
	if err == nil {
		t.Error("expected error, got nil")
	}
}

func TestReturnsErrWhenEndpointsAreNil(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)
	tunnel := Tunnel{}
	_, err := Connect(context.Background(), logger, &tunnel, "")
	if err == nil {
		t.Error("expected error, got nil")
	}
}

func TestReturnsErrWhenTunnelEndpointsDontMatchHostID(t *testing.T) {
	tunnel := Tunnel{
		Endpoints: []*TunnelEndpoint{
			{
				HostID: "host1",
			},
		},
	}

	logger := log.New(os.Stdout, "", log.LstdFlags)
	_, err := Connect(context.Background(), logger, &tunnel, "host2")
	if err == nil {
		t.Error("expected error, got nil")
	}
}

func TestReturnsErrWhenEndpointGroupsContainMultipleHosts(t *testing.T) {
	tunnel := Tunnel{
		Endpoints: []*TunnelEndpoint{
			{
				HostID: "host1",
			},
			{
				HostID: "host2",
			},
		},
	}

	logger := log.New(os.Stdout, "", log.LstdFlags)
	_, err := Connect(context.Background(), logger, &tunnel, "host1")
	if err == nil {
		t.Error("expected error, got nil")
	}
}

func TestReturnsErrWhenThereAreMoreThanOneEndpoints(t *testing.T) {
	tunnel := Tunnel{
		Endpoints: []*TunnelEndpoint{
			{
				HostID: "host1",
			},
			{
				HostID: "host1",
			},
		},
	}

	logger := log.New(os.Stdout, "", log.LstdFlags)
	_, err := Connect(context.Background(), logger, &tunnel, "")
	if err == nil {
		t.Error("expected error, got nil")
	}
}
