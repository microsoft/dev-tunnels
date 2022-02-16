package tunnels

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"testing"
)

func getAccessToken() string {
	return ""
}

func TestTunnelCreate(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

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
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
	}
}
