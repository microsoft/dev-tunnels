// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package goTunnels

import "fmt"

var (
	allScopes = map[TunnelAccessScope]bool{
		TunnelAccessScopeManage:  true,
		TunnelAccessScopeHost:    true,
		TunnelAccessScopeInspect: true,
		TunnelAccessScopeConnect: true,
	}
)

func (s *TunnelAccessScopes) valid(validScopes []TunnelAccessScope) error {
	if s == nil {
		return fmt.Errorf("scopes cannot be null")
	}
	for _, scope := range *s {
		if len(scope) == 0 {
			return fmt.Errorf("scope cannot be null")
		} else if !allScopes[scope] {
			return fmt.Errorf("invalid scope %s", scope)
		}
	}
	if len(validScopes) > 0 {
		for _, scope := range *s {
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
