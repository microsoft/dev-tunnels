package tunnels

import "fmt"

type TunnelPort struct {
	ClusterID     string             `json:"ClusterId,omitempty"`
	TunnelID      string             `json:"TunnelId,omitempty"`
	PortNumber    int                `json:"PortNumber,omitempty"`
	Protocol      TunnelPortProtocol `json:"Protocol,omitempty"`
	AccessTokens  map[string]string
	AccessControl *TunnelAccessControl
	Options       *TunnelOptions
	Status        *TunnelStatus
}

type TunnelPortProtocol string

const (
	TunnelPortProtocolAuto  TunnelPortProtocol = "auto"
	TunnelPortProtocolTcp   TunnelPortProtocol = "tcp"
	TunnelPortProtocolUdp   TunnelPortProtocol = "udp"
	TunnelPortProtocolSsh   TunnelPortProtocol = "ssh"
	TunnelPortProtocolRdp   TunnelPortProtocol = "rdp"
	TunnelPortProtocolHttp  TunnelPortProtocol = "http"
	TunnelPortProtocolHttps TunnelPortProtocol = "https"
)

func NewTunnelPort(portNumber int, clusterId string, tunnelId string, protocol TunnelPortProtocol) *TunnelPort {
	port := &TunnelPort{
		PortNumber: portNumber,
		ClusterID:  clusterId,
		TunnelID:   tunnelId,
		Protocol:   protocol,
	}
	if len(port.Protocol) == 0 {
		port.Protocol = TunnelPortProtocolAuto
	}
	port.AccessTokens = make(map[string]string)
	port.AccessControl = &TunnelAccessControl{}
	port.Options = &TunnelOptions{}
	port.Status = &TunnelStatus{}
	return port
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
	if tunnelPort.AccessControl != nil {
		for _, entry := range tunnelPort.AccessControl.Entries {
			if !entry.IsInherited {
				convertedPort.AccessControl.Entries = append(convertedPort.AccessControl.Entries, entry)
			}
		}
	}

	return convertedPort, nil
}
