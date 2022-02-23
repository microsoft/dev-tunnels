package tunnels

import (
	"fmt"
	"time"

	"github.com/rodaine/table"
)

const PackageVersion = "0.0.1"

type Tunnel struct {
	ClusterID     string `json:"ClusterId,omitempty"`
	TunnelID      string `json:"TunnelId,omitempty"`
	Name          string `json:"Name,omitempty"`
	Description   string `json:"Description,omitempty"`
	Tags          []string
	Domain        string `json:"Domain,omitempty"`
	AccessTokens  map[TunnelAccessScope]string
	AccessControl *TunnelAccessControl
	Options       *TunnelOptions
	Status        *TunnelStatus
	Endpoints     []*TunnelEndpoint
	Ports         []*TunnelPort `json:"Ports,omitempty"`
}

type TunnelAccessControl struct {
	Entries []*TunnelAccessControlEntry `json:"entries,omitempty"`
}

type TunnelAccessControlEntry struct {
	Type         TunnelAccessControlEntryType
	IsInherited  bool
	IsDeny       bool
	Subjects     []string
	Scopes       []TunnelAccessScope
	Provider     string
	Organization string
}

type TunnelAccessControlEntryType string

const (
	TunnelAccessControlEntryTypeNone            TunnelAccessControlEntryType = "none"
	TunnelAccessControlEntryTypeAnonymous       TunnelAccessControlEntryType = "anonymous"
	TunnelAccessControlEntryTypeUsers           TunnelAccessControlEntryType = "users"
	TunnelAccessControlEntryTypeGroups          TunnelAccessControlEntryType = "groups"
	TunnelAccessControlEntryTypeOrganizations   TunnelAccessControlEntryType = "organizations"
	TunnelAccessControlEntryTypeRepositories    TunnelAccessControlEntryType = "repositories"
	TunnelAccessControlEntryTypePublicKeys      TunnelAccessControlEntryType = "publickeys"
	TunnelAccessControlEntryTypeIPAddressRanges TunnelAccessControlEntryType = "ipaddressranges"
)

type TunnelOptions struct {
	ConnectionModes []TunnelConnectionMode
}

type TunnelConnectionMode string

const (
	TunnelConnectionModeLocalNetwork   TunnelConnectionMode = "LocalNetwork"
	TunnelConnectionModeTunnelRelay    TunnelConnectionMode = "TunnelRelay"
	TunnelConnectionModeLiveShareRelay TunnelConnectionMode = "LiveShareRelay"
)

type TunnelStatus struct {
	HostConectionCount       int
	LastHostConnectionTime   time.Time
	ClientConnectionCount    int
	LastClientConnectionTime time.Time
}

type TunnelEndpoint struct {
	ConnectionMode TunnelConnectionMode
	HostID         string
	PortURIFormat  string
	HostRelayURI   string
	ClientRelayURI string
	HostPublicKeys []string
}

func (tunnel *Tunnel) requestObject() (*Tunnel, error) {
	if tunnel.AccessControl != nil && tunnel.AccessControl.Entries != nil {
		for _, access := range tunnel.AccessControl.Entries {
			if access.IsInherited {
				return nil, fmt.Errorf("tunnel access control cannot include inherited entries")
			}
		}
	}

	convertedTunnel := &Tunnel{
		Name:          tunnel.Name,
		Domain:        tunnel.Domain,
		Description:   tunnel.Description,
		Tags:          tunnel.Tags,
		Options:       tunnel.Options,
		AccessControl: tunnel.AccessControl,
		Endpoints:     tunnel.Endpoints,
		Ports:         make([]*TunnelPort, 0),
	}

	for _, port := range tunnel.Ports {
		convertedPort, err := port.requestObject(tunnel)
		if err != nil {
			return nil, err
		}
		convertedTunnel.Ports = append(convertedTunnel.Ports, convertedPort)
	}
	return convertedTunnel, nil
}

func (t *Tunnel) table() table.Table {
	tbl := table.New(" ", " ")

	var accessTokens string
	for scope := range t.AccessTokens {
		if len(accessTokens) == 0 {
			accessTokens += string(scope)
		} else {
			accessTokens += fmt.Sprintf(", %s", scope)
		}
	}

	var ports string
	for _, port := range t.Ports {
		if len(ports) == 0 {
			ports += fmt.Sprintf("%d - %s", port.PortNumber, port.Protocol)
		} else {
			ports += fmt.Sprintf(", %d - %s", port.PortNumber, port.Protocol)
		}
	}
	tbl.AddRow("ClusterId", t.ClusterID)
	tbl.AddRow("TunnelId", t.TunnelID)
	tbl.AddRow("Name", t.Name)
	tbl.AddRow("Description", t.Description)
	tbl.AddRow("Tags", fmt.Sprintf("%v", t.Tags))
	if t.AccessControl != nil {
		tbl.AddRow("Access Control", fmt.Sprintf("%v", *t.AccessControl))
	}
	tbl.AddRow("Ports", ports)
	tbl.AddRow("Host Connections", t.Status.HostConectionCount)
	tbl.AddRow("Client Connections", t.Status.ClientConnectionCount)
	tbl.AddRow("Available Scopes", accessTokens)
	return tbl
}
