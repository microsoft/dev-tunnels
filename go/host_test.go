package tunnels

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"testing"

	tunnelssh "github.com/microsoft/tunnels/go/ssh"
)

var (
	logger = log.New(os.Stdout, "", log.LstdFlags)
)

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

	portToAdd := NewTunnelPort(3000, "", "", "auto")
	port, err := managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}

	logger.Println(fmt.Sprintf("Created port: %+v", *port))
	host, _ := NewHost(managementClient, logger)
	ssh := &tunnelssh.SSHSession{}
	host.sshSessions[*ssh] = true
	logger.Println(host.manager.uri)
	ctx = context.Background()
	err = host.StartServer(ctx, createdTunnel)
	if err != nil {
		t.Fatal(err.Error())
		return
	}
}
