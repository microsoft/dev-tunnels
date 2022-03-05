package tunnels

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"testing"
)

var (
	logger = log.New(os.Stdout, "", log.LstdFlags)
)

// This tests against the real prod service uri when it creates and manages tunnels
// We do have a cleanup worker that will delete unused tunnels so its not horrible for the test to break mid run
func TestSuccessfulHost(t *testing.T) {
	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}
	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{
		TokenScopes: hostOrManageAccessTokenScope,
	}

	// Create the tunnel for the test
	createdTunnel, err := managementClient.CreateTunnel(ctx, tunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
		createdTunnel.table().Print()
	}

	// Add a port to the tunnel
	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Created port: %+v", *port))

	host, _ := NewHost(managementClient, logger)
	logger.Println(host.manager.uri)

	ctx = context.Background()
	err = host.StartServer(ctx, createdTunnel)
	if err != nil {
		t.Errorf(err.Error())
		return
	}

	// Delete tunnel after test is finished
	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}
