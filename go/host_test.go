package tunnels

import (
	"context"
	"log"
	"net/url"
	"os"
	"testing"

	tunnelssh "github.com/microsoft/tunnels/go/ssh"
)

const (
	uri = "https://global.rel.tunnels.api.visualstudio.com/"
)

var (
	ctx    = context.Background()
	logger = log.New(os.Stdout, "", log.LstdFlags)
)

func getAccessToken() string {
	return ""
}

func TestSuccessfulHost(t *testing.T) {
	url, err := url.Parse(uri)
	if err != nil {
		t.Errorf(err.Error())
	}

	managementClient, err := NewManager("Tunnels-Go-SDK", getAccessToken, url, nil)
	if err != nil {
		t.Errorf(err.Error())
	}
	host, _ := NewHost(managementClient)
	ssh := &tunnelssh.SSHSession{}
	host.sshSessions[*ssh] = true
	logger.Println(host.manager.uri)
}
