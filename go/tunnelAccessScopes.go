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
		return fmt.Errorf("scopes cannot be null")
	}
	for _, scope := range scopes {
		if len(scope) == 0 {
			return fmt.Errorf("scope cannot be null")
		} else if !contains(AllScopes, scope) {
			return fmt.Errorf("invalid scope %s", scope)
		}
	}
	if len(validScopes) > 0 {
		for _, scope := range scopes {
			if !contains(validScopes, scope) {
				return fmt.Errorf("tunnel access scope is invalid for current request: %s", scope)
			}
		}
	}
	return nil
}
