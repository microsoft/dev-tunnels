package tunnels

import "sync"

type forwardedPorts struct {
	portsMu sync.RWMutex
	ports   map[int]bool

	notify chan forwardedPortNotification
}

type forwardedPortNotification struct {
	port             int
	notificationType forwardedPortNotificationType
}

type forwardedPortNotificationType int

const (
	forwardedPortNotificationTypeAdd forwardedPortNotificationType = iota
	forwardedPortNotificationTypeRemove
)

func newForwardedPorts() *forwardedPorts {
	return &forwardedPorts{
		ports:  make(map[int]bool),
		notify: make(chan forwardedPortNotification),
	}
}

func (r *forwardedPorts) Add(port int) {
	r.portsMu.Lock()
	defer r.portsMu.Unlock()

	r.ports[port] = true

	notification := forwardedPortNotification{
		port:             port,
		notificationType: forwardedPortNotificationTypeAdd,
	}

	select {
	case r.notify <- notification:
	default:
	}
}

func (r *forwardedPorts) hasPort(port int) bool {
	r.portsMu.RLock()
	defer r.portsMu.RUnlock()

	return r.ports[port]
}
