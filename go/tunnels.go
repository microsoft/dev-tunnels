package tunnels

import (
	"fmt"

	"github.com/rodaine/table"
)

const PackageVersion = "0.0.1"

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
	tbl.AddRow("Host Connections", t.Status.HostConnectionCount)
	tbl.AddRow("Client Connections", t.Status.ClientConnectionCount)
	tbl.AddRow("Available Scopes", accessTokens)
	return tbl
}

func NewTunnelPort(portNumber uint16, clusterId string, tunnelId string, protocol TunnelProtocol) *TunnelPort {
	port := &TunnelPort{
		PortNumber: portNumber,
		ClusterID:  clusterId,
		TunnelID:   tunnelId,
		Protocol:   string(protocol),
	}
	if len(port.Protocol) == 0 {
		port.Protocol = string(TunnelProtocolAuto)
	}
	port.AccessTokens = make(map[TunnelAccessScope]string)
	port.AccessControl = &TunnelAccessControl{}
	port.Options = &TunnelOptions{}
	port.Status = &TunnelPortStatus{}
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
