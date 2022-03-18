package tunnels

import "sync"

type remoteForwardedPorts struct {
	portsMu sync.RWMutex
	ports   map[int]bool

	notify chan remoteForwardedPortNotification
}

type remoteForwardedPortNotification struct {
	port             int
	notificationType remoteForwardedPortNotificationType
}

type remoteForwardedPortNotificationType int

const (
	remoteForwardedPortNotificationTypeAdd remoteForwardedPortNotificationType = iota
	remoteForwardedPortNotificationTypeRemove
)

func newRemoteForwardedPorts() *remoteForwardedPorts {
	return &remoteForwardedPorts{
		ports:  make(map[int]bool),
		notify: make(chan remoteForwardedPortNotification),
	}
}

func (r *remoteForwardedPorts) Add(port int) {
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

func (r *remoteForwardedPorts) HasPort(port int) bool {
	r.portsMu.RLock()
	defer r.portsMu.RUnlock()

	return r.ports[port]
}

func (r *remoteForwardedPorts) Remove(port int) {
	r.portsMu.Lock()
	defer r.portsMu.Unlock()

	r.ports[port] = false

	notification := remoteForwardedPortNotification{
		port:             port,
		notificationType: remoteForwardedPortNotificationTypeRemove,
	}

	select {
	case r.notify <- notification:
	default:
	}
}
