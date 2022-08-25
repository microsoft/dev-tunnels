// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"fmt"
	"strings"
)

var (
	allScopes = map[TunnelAccessScope]bool{
		TunnelAccessScopeManage:      true,
		TunnelAccessScopeManagePorts: true,
		TunnelAccessScopeHost:        true,
		TunnelAccessScopeInspect:     true,
		TunnelAccessScopeConnect:     true,
	}
)

func (s *TunnelAccessScopes) valid(validScopes []TunnelAccessScope, allowMultiple bool) error {
	if s == nil {
		return fmt.Errorf("scopes cannot be null")
	}

	var scopes TunnelAccessScopes
	if allowMultiple {
		for _, scope := range *s {
			for _, ss := range strings.Split(string(scope), " ") {
				scopes = append(scopes, TunnelAccessScope(ss))
			}
		}
	} else {
		scopes = *s
	}

	for _, scope := range scopes {
		if len(scope) == 0 {
			return fmt.Errorf("scope cannot be null")
		} else if !allScopes[scope] {
			return fmt.Errorf("invalid scope %s", scope)
		}
	}
	if len(validScopes) > 0 {
		for _, scope := range scopes {
			if !scopeContains(validScopes, scope) {
				return fmt.Errorf("tunnel access scope is invalid for current request: %s", scope)
			}
		}
	}
	return nil
}

func scopeContains(s []TunnelAccessScope, e TunnelAccessScope) bool {
	for _, a := range s {
		if a == e {
			return true
		}
	}
	return false
}
