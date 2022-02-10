package tunnels

import "fmt"

const (
	ManageScope  = "manage"
	HostScope    = "host"
	InspectScope = "inspect"
	ConnectScope = "connect"
)

var (
	AllScopes = []string{
		ManageScope,
		HostScope,
		InspectScope,
		ConnectScope,
	}
)

func ValidateScopes(scopes []string, validScopes []string) error {
	if scopes == nil {
		return fmt.Errorf("Scopes cannot be null")
	}
	for _, scope := range scopes {
		if len(scope) == 0 {
			return fmt.Errorf("Scope cannot be null")
		} else if !contains(AllScopes, scope) {
			return fmt.Errorf("Invalid scope %s", scope)
		}
	}
	if len(validScopes) > 0 {
		for _, scope := range scopes {
			if !contains(validScopes, scope) {
				return fmt.Errorf("Tunnel access scope is invalid for current request: %s", scope)
			}
		}
	}
	return nil
}
