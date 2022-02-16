package tunnels

import (
	"fmt"
	"net/url"
	"strings"
)

type TunnelRequestOptions struct {
	AccessToken       string
	AdditionalHeaders map[string]string
	FollowRedirects   bool
	IncludePorts      bool
	Scopes            TunnelAccessScopes
	TokenScopes       TunnelAccessScopes
	ForceRename       bool
}

func (options *TunnelRequestOptions) toQueryString() string {
	queryOptions := make(map[string]string)
	if options.IncludePorts {
		queryOptions["includePorts"] = "true"
	}
	if options.Scopes != nil {
		if err := options.Scopes.valid(nil); err == nil {
			queryOptions["scopes"] = options.Scopes.join(",")
		}
	}
	if options.TokenScopes != nil {
		if err := options.TokenScopes.valid(nil); err == nil {
			queryOptions["tokenScopes"] = options.TokenScopes.join(",")
		}
	}
	if options.ForceRename {
		queryOptions["forceRename"] = "true"
	}
	querySlice := make([]string, 0)
	for key, value := range queryOptions {
		querySlice = append(querySlice, fmt.Sprintf("%s=%s", key, url.QueryEscape(value)))
	}
	return strings.Join(querySlice, "&")
}
