package tunnels

import (
	"time"
)

type Tunnel struct {
	ClusterID     string
	TunnelID      string
	Name          string
	Description   string
	Tags          []string
	Domain        string
	AccessTokens  map[TunnelAccessScope]string
	AccessControl *TunnelAccessControl
	Options       *TunnelOptions
	Status        *TunnelStatus
	Endpoints     []*TunnelEndpoint
	Ports         []*TunnelPort
}

type TunnelAccessScope string

const (
	TunnelAccessScopeManage  TunnelAccessScope = "manage"
	TunnelAccessScopeHost    TunnelAccessScope = "host"
	TunnelAccessScopeInspect TunnelAccessScope = "inspect"
	TunnelAccessScopeConnect TunnelAccessScope = "connect"
)

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
