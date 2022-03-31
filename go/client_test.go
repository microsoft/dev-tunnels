package tunnels

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"net/url"
	"os"
	"strings"
	"testing"
	"time"

	"github.com/microsoft/tunnels/go/ssh/messages"

	tunnelstest "github.com/microsoft/tunnels/go/test"
)

func TestSuccessfulConnect(t *testing.T) {
	accessToken := "access-token"
	relayServer, err := tunnelstest.NewRelayServer(
		tunnelstest.WithAccessToken(accessToken),
	)
	if err != nil {
		t.Fatal(err)
	}

	hostURL := strings.Replace(relayServer.URL(), "http://", "ws://", 1)
	tunnel := Tunnel{
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeConnect: accessToken,
		},
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

func TestReturnsErrWithInvalidAccessToken(t *testing.T) {
	accessToken := "access-token"
	relayServer, err := tunnelstest.NewRelayServer(
		tunnelstest.WithAccessToken(accessToken),
	)
	if err != nil {
		t.Fatal(err)
	}

	hostURL := strings.Replace(relayServer.URL(), "http://", "ws://", 1)
	tunnel := Tunnel{
		AccessTokens: map[TunnelAccessScope]string{
			TunnelAccessScopeConnect: "invalid-access-token",
		},
		Endpoints: []*TunnelEndpoint{
			{
				HostID:         "host1",
				ClientRelayURI: hostURL,
			},
		},
	}

	logger := log.New(os.Stdout, "", log.LstdFlags)
	_, err = Connect(context.Background(), logger, &tunnel, "")
	if err == nil {
		t.Error("expected error, got nil")
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

func TestPortForwarding(t *testing.T) {
	listen, err := net.Listen("tcp", "127.0.0.1:8000")
	if err != nil {
		t.Fatal(fmt.Errorf("failed to listen: %v", err))
	}
	defer listen.Close()

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	streamPort := 8001
	streamData := "stream-data"
	stream := bytes.NewBufferString(streamData)
	pfsChannel := messages.NewPortForwardChannel(1, "127.0.0.1", uint32(streamPort), "", 0)
	relayServer, err := tunnelstest.NewRelayServer(
		tunnelstest.WithForwardedStream(pfsChannel, streamPort, stream),
	)
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

	ctx, cancel = context.WithTimeout(ctx, 5*time.Second)
	defer cancel()

	logger := log.New(os.Stdout, "", log.LstdFlags)
	done := make(chan error)
	go func() {
		c, err := Connect(ctx, logger, &tunnel, "")
		if err != nil {
			done <- fmt.Errorf("connect failed: %v", err)
			return
		}
		if c == nil {
			done <- errors.New("nil connection")
			return
		}

		if err := relayServer.ForwardPort(ctx, streamPort); err != nil {
			done <- fmt.Errorf("forward port failed: %v", err)
			return
		}

		if err := c.WaitForForwardedPort(ctx, streamPort); err != nil {
			done <- fmt.Errorf("wait for forwarded port failed: %v", err)
			return
		}

		if err := c.WaitForForwardedPort(ctx, streamPort); err != nil {
			done <- fmt.Errorf("wait for forwarded port failed: %v", err)
			return
		}

		done <- c.ConnectToForwardedPort(ctx, listen, streamPort)
	}()

	go func() {
		var conn net.Conn
		retries := 0
		for conn == nil && retries < 2 {
			conn, err = net.DialTimeout("tcp", ":8000", 2*time.Second)
			time.Sleep(1 * time.Second)
		}
		if conn == nil {
			done <- errors.New("failed to connect to forwarded port")
		}
		b := make([]byte, len(streamData))
		if _, err := conn.Read(b); err != nil && err != io.EOF {
			done <- fmt.Errorf("reading stream: %w", err)
		}
		if string(b) != streamData {
			done <- fmt.Errorf("stream data is not expected value, got: %s", string(b))
		}
		if _, err := conn.Write([]byte("new-data")); err != nil {
			done <- fmt.Errorf("writing to stream: %w", err)
		}
		done <- nil
	}()

	select {
	case <-ctx.Done():
		t.Fatal("test timed out")
	case err := <-relayServer.Err():
		t.Errorf("relay server error: %v", err)
	case err := <-done:
		if err != nil {
			t.Errorf(err.Error())
		}
	}
}

func TestSuccessfulConnectJake(t *testing.T) {
	ctx := context.Background()
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager(userAgentManagerTest, getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	options := &TunnelRequestOptions{IncludePorts: true, Scopes: []TunnelAccessScope{"connect"}, TokenScopes: []TunnelAccessScope{"connect"}}

	newTunnel := &Tunnel{
		TunnelID:  "hjmgvccn",
		ClusterID: "usw2",
	}
	getTunnel, err := managementClient.GetTunnel(ctx, newTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	done := make(chan error)
	go func() {
		fmt.Println("hello")
		c, err := Connect(context.Background(), logger, getTunnel, "")
		if err != nil {
			done <- fmt.Errorf("connect failed: %v", err)
			return
		}
		if c == nil {
			done <- errors.New("nil connection")
			return
		}

		listen, err := net.Listen("tcp", ":5001")
		if err != nil {
			done <- fmt.Errorf("failed to listen: %v", err)
		}
		defer listen.Close()
		go c.ConnectToForwardedPort(ctx, listen, 5000)

		c.WaitForForwardedPort(ctx, 5000)
		time.Sleep(2 * time.Minute)
		done <- nil
	}()

	select {
	case err := <-done:
		if err != nil {
			t.Errorf(err.Error())
		}
	}
}
