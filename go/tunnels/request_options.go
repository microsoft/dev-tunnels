// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"fmt"
	"net/url"
)

// Options that are sent in requests to the tunnels service.
type TunnelRequestOptions struct {
	// Token used for authentication for service.
	AccessToken string

	// Additional headers to be included in the request.
	AdditionalHeaders map[string]string

	// Additional qurey parameters to be included in the request.
	AdditionalQueryParameters map[string]string

	// Indicates whether HTTP redirect responses will be automatically followed.
	FollowRedirects bool

	// Flag that requests tunnel ports when retrieving a tunnel object.
	IncludePorts bool

	// Flag that requests tunnel access control details when listing or searching tunnels.
	IncludeAccessControl bool

	// Optional list of labels to filter the requested tunnels or ports.
	// By default, an item is included if ANY label matches; set `requireAllLabels` to match
	// ALL labels instead.
	Labels []string

	// Flag that indicates whether listed items must match all labels specified in `labels`.
	// If false, an item is included if any tag matches.
	RequireAllLabels bool

	// List of token scopes that are requested when retrieving a tunnel or tunnel port object.
	TokenScopes TunnelAccessScopes

	// If there is another tunnel with the name requested in updateTunnel, try to acquire the name from the other tunnel.
	ForceRename bool

	// Limits the number of tunnels returned when searching or listing tunnels.
	Limit uint
}

func (options *TunnelRequestOptions) queryString() string {
	queryOptions := url.Values{}
	if options.IncludePorts {
		queryOptions.Set("includePorts", "true")
	}
	if options.IncludeAccessControl {
		queryOptions.Set("includeAccessControl", "true")
	}

	if options.TokenScopes != nil {
		if err := options.TokenScopes.valid(nil, true); err == nil {
			for _, scope := range options.TokenScopes {
				queryOptions.Add("tokenScopes", string(scope))
			}
		}
	}
	if options.ForceRename {
		queryOptions.Set("forceRename", "true")
	}
	if options.Labels != nil {
		for _, label := range options.Labels {
			queryOptions.Add("labels", string(label))
		}

		if options.RequireAllLabels {
			queryOptions.Set("allLabels", "true")
		}
	}

	if options.AdditionalQueryParameters != nil {
		for paramName, paramValue := range options.AdditionalQueryParameters {
			queryOptions.Add(paramName, paramValue)
		}
	}

	if options.Limit > 0 {
		queryOptions.Set("limit", fmt.Sprint(options.Limit))
	}

	return queryOptions.Encode()
}
