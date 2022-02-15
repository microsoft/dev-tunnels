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
	Scopes            []string
	TokenScopes       []string
	ForceRename       bool
}

func (options *TunnelRequestOptions) toQueryString() string {
	var queryOptions map[string]string
	if options.IncludePorts {
		queryOptions["includePorts"] = "true"
	}
	if options.Scopes != nil {
		ValidateScopes(options.Scopes, nil)
		queryOptions["scopes"] = strings.Join(options.Scopes, ",")
	}
	if options.TokenScopes != nil {
		ValidateScopes(options.TokenScopes, nil)
		queryOptions["tokenScopes"] = strings.Join(options.TokenScopes, ",")
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
