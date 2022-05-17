// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import "sync"

type remoteForwardedPorts struct {
	portsMu sync.RWMutex
	ports   map[uint16]bool

	notify chan remoteForwardedPortNotification
}

type remoteForwardedPortNotification struct {
	port             uint16
	notificationType remoteForwardedPortNotificationType
}

type remoteForwardedPortNotificationType int

const (
	remoteForwardedPortNotificationTypeAdd remoteForwardedPortNotificationType = iota
	remoteForwardedPortNotificationTypeRemove
)

func newRemoteForwardedPorts() *remoteForwardedPorts {
	return &remoteForwardedPorts{
		ports:  make(map[uint16]bool),
		notify: make(chan remoteForwardedPortNotification),
	}
}

func (r *remoteForwardedPorts) Add(port uint16) {
	r.portsMu.Lock()
	defer r.portsMu.Unlock()

	r.ports[port] = true

	notification := remoteForwardedPortNotification{
		port:             port,
		notificationType: remoteForwardedPortNotificationTypeAdd,
	}

	select {
	case r.notify <- notification:
	default:
	}
}

func (r *remoteForwardedPorts) hasPort(port uint16) bool {
	r.portsMu.RLock()
	defer r.portsMu.RUnlock()

	return r.ports[port]
}
