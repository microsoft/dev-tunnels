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
	}

	if tunnel.Ports != nil {
		var convertedPorts []TunnelPort
		for _, port := range *tunnel.Ports {
			convertedPort, err := port.requestObject(tunnel)
			if err != nil {
				return nil, err
			}
			convertedPorts = append(convertedPorts, *convertedPort)
		}
		convertedTunnel.Ports = &convertedPorts
	}
	return convertedTunnel, nil
}

func (t *Tunnel) table() table.Table {
	tbl := table.New("Tunnel Properties", " ")

	var accessTokens string
	if t.AccessTokens != nil {
		for scope := range *t.AccessTokens {
			if len(accessTokens) == 0 {
				accessTokens += string(scope)
			} else {
				accessTokens += fmt.Sprintf(", %s", scope)
			}
		}
	}

	var ports string
	if t.Ports != nil {
		for _, port := range *t.Ports {
			if port.PortNumber != nil && port.Protocol != nil {
				if len(ports) == 0 {
					ports += fmt.Sprintf("%d - %s", *port.PortNumber, *port.Protocol)
				} else {
					ports += fmt.Sprintf(", %d - %s", *port.PortNumber, *port.Protocol)
				}
			}
		}
	}
	tbl.AddRow("ClusterId", *t.ClusterID)
	tbl.AddRow("TunnelId", *t.TunnelID)
	tbl.AddRow("Name", *t.Name)
	tbl.AddRow("Description", *t.Description)
	tbl.AddRow("Tags", fmt.Sprintf("%v", *t.Tags))
	if t.AccessControl != nil {
		tbl.AddRow("Access Control", fmt.Sprintf("%v", *t.AccessControl))
	}
	tbl.AddRow("Ports", ports)
	tbl.AddRow("Host Connections", t.Status.HostConnectionCount)
	tbl.AddRow("Client Connections", t.Status.ClientConnectionCount)
	tbl.AddRow("Available Scopes", accessTokens)
	return tbl
}

func (tp *TunnelPort) table() table.Table {
	tbl := table.New("TunnelPort Properties", " ")

	var accessTokens string
	if tp.AccessTokens != nil {
		for scope := range *tp.AccessTokens {
			if len(accessTokens) == 0 {
				accessTokens += string(scope)
			} else {
				accessTokens += fmt.Sprintf(", %s", scope)
			}
		}
	}

	tbl.AddRow("ClusterId", *tp.ClusterID)
	tbl.AddRow("TunnelId", *tp.TunnelID)
	tbl.AddRow("PortNumber", *tp.PortNumber)
	tbl.AddRow("Protocol", *tp.Protocol)
	if tp.AccessControl != nil {
		tbl.AddRow("Access Control", fmt.Sprintf("%v", *tp.AccessControl))
	}
	tbl.AddRow("Client Connections", tp.Status.ClientConnectionCount)
	tbl.AddRow("Last Connection Time", tp.Status.LastClientConnectionTime)
	return tbl
}

func NewTunnelPort(portNumber uint16, clusterId *string, tunnelId *string, protocol TunnelProtocol) *TunnelPort {
	protocolValue := string(protocol)
	if len(protocolValue) == 0 {
		protocolValue = string(TunnelProtocolAuto)
	}
	port := &TunnelPort{
		PortNumber: &portNumber,
		ClusterID:  clusterId,
		TunnelID:   tunnelId,
		Protocol:   &protocolValue,
	}
	return port
}

func (tunnelPort *TunnelPort) requestObject(tunnel *Tunnel) (*TunnelPort, error) {
	if tunnelPort.ClusterID != nil && tunnel.ClusterID != nil && *tunnelPort.ClusterID != *tunnel.ClusterID {
		return nil, fmt.Errorf("tunnel port cluster ID '%s' does not match tunnel cluster ID '%s'", *tunnelPort.ClusterID, *tunnel.ClusterID)
	}
	if tunnelPort.TunnelID != nil && tunnel.TunnelID != nil && *tunnelPort.TunnelID != *tunnel.TunnelID {
		return nil, fmt.Errorf("tunnel port tunnel ID does not match tunnel")
	}
	convertedPort := &TunnelPort{
		PortNumber: tunnelPort.PortNumber,
		Protocol:   tunnelPort.Protocol,
		Options:    tunnelPort.Options,
	}
	if tunnelPort.AccessControl != nil {
		var newEntries []TunnelAccessControlEntry
		for _, entry := range tunnelPort.AccessControl.Entries {
			if !entry.IsInherited {
				newEntries = append(newEntries, entry)
			}
		}
		convertedPort.AccessControl = &TunnelAccessControl{
			Entries: newEntries,
		}
	}

	return convertedPort, nil
}
