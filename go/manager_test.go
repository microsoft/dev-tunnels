package tunnels

import (
	"context"
	"net/url"
	"testing"
)

func getAccessToken() <-chan string {
	r := make(chan string)
	go func() {
		defer close(r)
		r <- ""
	}()
	return r
}

func TestTunnelCreate(t *testing.T) {
	url, err := url.Parse("https://global.ci.tunnels.dev.api.visualstudio.com/")
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	}
}
