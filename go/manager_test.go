package tunnels

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"testing"
)

const (
	uri = "https://global.rel.tunnels.api.visualstudio.com/"
)

func getAccessToken() string {
	return ""
}

func TestListTunnels(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	// This test requires authentication
	if getAccessToken() == "" {
		return
	}
	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	options := &TunnelRequestOptions{}
	tunnels, err := managementClient.ListTunnels(context.Background(), "", "", options)
	if err != nil {
		t.Errorf(err.Error())
	}
	for _, tunnel := range tunnels {
		logger.Println(fmt.Sprintf("found tunnel with id %s", tunnel.TunnelID))
		tunnel.table().Print()
	}

}

func TestTunnelCreateDelete(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
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
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
		createdTunnel.table().Print()
	}

	err = managementClient.DeleteTunnel(context.Background(), createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}

func TestTunnelCreateGetDelete(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
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
		return
	}
	if createdTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully created")
	} else {
		logger.Println(fmt.Sprintf("Created tunnel with id %s", createdTunnel.TunnelID))
		createdTunnel.table().Print()
	}

	getTunnel, err := managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	err = managementClient.DeleteTunnel(context.Background(), createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", getTunnel.TunnelID))
	}
}

func TestTunnelAddPort(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
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
	portToAdd := &TunnelPort{PortNumber: 3000, Protocol: "auto"}
	port, err := managementClient.CreateTunnelPort(context.Background(), createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Created port: %+v", *port))

	getTunnel, err := managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
		getTunnel.table().Print()
	}

	err = managementClient.DeleteTunnel(context.Background(), createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}

func TestTunnelDeletePort(t *testing.T) {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}

	tunnel := &Tunnel{}
	options := &TunnelRequestOptions{IncludePorts: true}
	createdTunnel, err := managementClient.CreateTunnel(context.Background(), tunnel, options)
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
	portToAdd := &TunnelPort{PortNumber: 3000, Protocol: "auto"}
	port, err := managementClient.CreateTunnelPort(context.Background(), createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Created port: %+v", *port))

	getTunnel, err := managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
		getTunnel.table().Print()
	}

	err = managementClient.DeleteTunnelPort(context.Background(), createdTunnel, 3000, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Deleted port: %+v", *port))

	getTunnel, err = managementClient.GetTunnel(context.Background(), createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
		getTunnel.table().Print()
	}

	err = managementClient.DeleteTunnel(context.Background(), createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}
