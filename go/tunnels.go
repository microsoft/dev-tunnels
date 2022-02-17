package tunnels

import (
	"fmt"
	"time"
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
	Ports         []*TunnelPort
}

type TunnelAccessControl struct {
	Entries []*TunnelAccessControlEntry
}

type TunnelAccessControlEntry struct {
	Type         TunnelAccessControlEntryType
	IsInherited  bool
	IsDeny       bool
	Subjects     []string
	Scopes       []string
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

type TunnelPort struct {
	ClusterID     string
	TunnelID      string
	PortNumber    int
	Protocol      string
	AccessTokens  map[string]string
	AccessControl *TunnelAccessControl
	Options       *TunnelOptions
	Status        *TunnelStatus
}

func (tunnelPort *TunnelPort) requestObject(tunnel *Tunnel) (*TunnelPort, error) {
	if tunnelPort.ClusterID != "" && tunnel.ClusterID != "" && tunnelPort.ClusterID != tunnel.ClusterID {
		return nil, fmt.Errorf("tunnel port cluster ID does not match tunnel")
	}
	if tunnelPort.TunnelID != "" && tunnel.TunnelID != "" && tunnelPort.TunnelID != tunnel.TunnelID {
		return nil, fmt.Errorf("tunnel port tunnel ID does not match tunnel")
	}
	convertedPort := &TunnelPort{
		PortNumber:    tunnelPort.PortNumber,
		Protocol:      tunnelPort.Protocol,
		Options:       tunnel.Options,
		AccessControl: &TunnelAccessControl{},
	}
	for _, entry := range tunnelPort.AccessControl.Entries {
		if !entry.IsInherited {
			convertedPort.AccessControl.Entries = append(convertedPort.AccessControl.Entries, entry)
		}
	}
	return convertedPort, nil
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
