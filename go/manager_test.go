package tunnels

import (
	"context"
	"fmt"
	"log"
	"math/rand"
	"net/url"
	"os"
	"testing"
	"time"
)

const (
	uri = "https://global.rel.tunnels.api.visualstudio.com/"
)

var (
	ctx = context.Background()
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
	tunnels, err := managementClient.ListTunnels(ctx, "", "", options)
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

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}

func TestTunnelCreateUpdateDelete(t *testing.T) {
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
	s1 := rand.NewSource(time.Now().UnixNano())
	r1 := rand.New(s1)
	generatedName := fmt.Sprintf("test%d", r1.Intn(10000))
	createdTunnel.Name = generatedName
	updatedTunnel, err := managementClient.UpdateTunnel(ctx, createdTunnel, options)
	if err != nil || updatedTunnel.Name != generatedName {
		t.Errorf("tunnel was not successfully updated")
	} else {
		logger.Println("Tunnel updated")
		updatedTunnel.table().Print()
	}
	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

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

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if getTunnel.TunnelID == "" {
		t.Errorf("tunnel was not successfully found")
	} else {
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

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

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
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

	if len(getTunnel.Ports) != 1 {
		t.Errorf("port was not successfully added to tunnel")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

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

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
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

	err = managementClient.DeleteTunnelPort(ctx, createdTunnel, 3000, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Deleted port: %+v", *port))

	getTunnel, err = managementClient.GetTunnel(ctx, createdTunnel, options)
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

	if len(getTunnel.Ports) != 0 {
		t.Errorf("port was not successfully deleted")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}

func TestTunnelUpdatePort(t *testing.T) {
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
	options := &TunnelRequestOptions{IncludePorts: true, Scopes: []TunnelAccessScope{"manage"}, TokenScopes: []TunnelAccessScope{"manage"}}
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

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
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
	accessEntry := &TunnelAccessControlEntry{
		Type:     TunnelAccessControlEntryTypeAnonymous,
		Subjects: []string{"test"},
		Scopes:   []TunnelAccessScope{"manage"},
	}
	portToAdd.AccessControl.Entries = append(port.AccessControl.Entries, accessEntry)

	port, err = managementClient.UpdateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf("port was not successfully updated")
	} else if len(port.AccessControl.Entries) != 1 {
		t.Errorf("port was not successfully updated")
	}

	getTunnel, err = managementClient.GetTunnel(ctx, createdTunnel, options)
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
	if len(getTunnel.Ports[0].AccessControl.Entries) != 1 {
		t.Errorf("tunnel port was not successfully updated, access control was not changed")
	}

	port, err = managementClient.GetTunnelPort(ctx, createdTunnel, 3000, options)
	if err != nil {
		t.Errorf("port get error %s", err.Error())
		return
	}
	if len(port.AccessControl.Entries) != 1 {
		t.Errorf("tunnel port was not successfully updated, access control was not changed")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}

func TestTunnelListPorts(t *testing.T) {
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

	portToAdd = NewTunnelPort(3001, "", "", "auto")
	port, err = managementClient.CreateTunnelPort(ctx, createdTunnel, portToAdd, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	logger.Println(fmt.Sprintf("Created port: %+v", *port))

	ports, err := managementClient.ListTunnelPorts(ctx, createdTunnel, options)
	if err != nil {
		t.Errorf(err.Error())
		return
	}
	if len(ports) != 2 {
		t.Errorf("ports not successfully listed")
	}
	for _, port := range ports {
		logger.Println(fmt.Sprintf("%+v", port))
	}

	getTunnel, err := managementClient.GetTunnel(ctx, createdTunnel, options)
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

	if len(getTunnel.Ports) != 2 {
		t.Errorf("port was not successfully added to tunnel")
	}

	err = managementClient.DeleteTunnel(ctx, createdTunnel, options)

	if err != nil {
		t.Errorf("tunnel was not successfully deleted")
	} else {
		logger.Println(fmt.Sprintf("Deleted tunnel with id %s", createdTunnel.TunnelID))
	}
}