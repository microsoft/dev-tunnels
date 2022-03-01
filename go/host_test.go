package tunnels

import (
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
	host, _ := NewHost(managementClient, logger)
	ssh := &tunnelssh.SSHSession{}
	host.sshSessions[*ssh] = true
	logger.Println(host.manager.uri)
}
