// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"encoding/json"
	"fmt"

	"github.com/rodaine/table"
)

const PackageVersion = "0.0.25"

func (tunnel *Tunnel) requestObject() (*Tunnel, error) {
	convertedTunnel := &Tunnel{
		Name:             tunnel.Name,
		Domain:           tunnel.Domain,
		Description:      tunnel.Description,
		Tags:             tunnel.Tags,
		Options:          tunnel.Options,
		Endpoints:        tunnel.Endpoints,
		CustomExpiration: tunnel.CustomExpiration,
	}
	if tunnel.AccessControl != nil {
		var newEntries []TunnelAccessControlEntry
		for _, entry := range tunnel.AccessControl.Entries {
			if !entry.IsInherited {
				newEntries = append(newEntries, entry)
			}
		}
		convertedTunnel.AccessControl = &TunnelAccessControl{
			Entries: newEntries,
		}
	}

	var convertedPorts []TunnelPort
	for _, port := range tunnel.Ports {
		convertedPort, err := port.requestObject(tunnel)
		if err != nil {
			return nil, err
		}
		convertedPorts = append(convertedPorts, *convertedPort)
	}
	convertedTunnel.Ports = convertedPorts

	return convertedTunnel, nil
}

func (t *Tunnel) Table() table.Table {
	tbl := table.New("Tunnel Properties", " ")

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

func (tp *TunnelPort) Table() table.Table {
	tbl := table.New("TunnelPort Properties", " ")

	var accessTokens string
	for scope := range tp.AccessTokens {
		if len(accessTokens) == 0 {
			accessTokens += string(scope)
		} else {
			accessTokens += fmt.Sprintf(", %s", scope)
		}
	}

	tbl.AddRow("ClusterId", tp.ClusterID)
	tbl.AddRow("TunnelId", tp.TunnelID)
	tbl.AddRow("PortNumber", tp.PortNumber)
	tbl.AddRow("Protocol", tp.Protocol)
	if tp.AccessControl != nil {
		tbl.AddRow("Access Control", fmt.Sprintf("%v", *tp.AccessControl))
	}
	tbl.AddRow("Client Connections", tp.Status.ClientConnectionCount)
	tbl.AddRow("Last Connection Time", tp.Status.LastClientConnectionTime)
	return tbl
}

func NewTunnelPort(portNumber uint16, clusterId string, tunnelId string, protocol TunnelProtocol) *TunnelPort {
	protocolValue := string(protocol)
	if len(protocolValue) == 0 {
		protocolValue = string(TunnelProtocolAuto)
	}
	port := &TunnelPort{
		PortNumber: portNumber,
		ClusterID:  clusterId,
		TunnelID:   tunnelId,
		Protocol:   protocolValue,
	}
	return port
}

func (tunnelPort *TunnelPort) requestObject(tunnel *Tunnel) (*TunnelPort, error) {
	if tunnelPort.ClusterID != "" && tunnel.ClusterID != "" && tunnelPort.ClusterID != tunnel.ClusterID {
		return nil, fmt.Errorf("tunnel port cluster ID '%s' does not match tunnel cluster ID '%s'", tunnelPort.ClusterID, tunnel.ClusterID)
	}
	if tunnelPort.TunnelID != "" && tunnel.TunnelID != "" && tunnelPort.TunnelID != tunnel.TunnelID {
		return nil, fmt.Errorf("tunnel port tunnel ID does not match tunnel")
	}
	convertedPort := &TunnelPort{
		PortNumber:  tunnelPort.PortNumber,
		Protocol:    tunnelPort.Protocol,
		IsDefault:   tunnelPort.IsDefault,
		Description: tunnelPort.Description,
		Tags:        tunnelPort.Tags,
		SshUser:     tunnelPort.SshUser,
		Options:     tunnelPort.Options,
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

func (rs *ResourceStatus) UnmarshalJSON(data []byte) (err error) {
	// First attempt to un-marshal as a ResourceStatus object.
	var obj map[string]uint64
	err = json.Unmarshal(data, &obj)
	if err == nil {
		rs.Current = obj["current"]
		rs.Limit = obj["limit"]
	} else {
		// It's not an object - unmarshal as a simple number.
		err = json.Unmarshal(data, &rs.Current)
		rs.Limit = 0
	}
	return err
}
