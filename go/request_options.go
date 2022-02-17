package tunnels

import (
	"net/url"
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

func (options *TunnelRequestOptions) queryString() string {
	queryOptions := url.Values{}
	if options.IncludePorts {
		queryOptions.Set("includePorts", "true")
	}
	if options.Scopes != nil {
		if err := options.Scopes.valid(nil); err == nil {
			for _, scope := range options.Scopes {
				queryOptions.Add("scopes", string(scope))
			}

		}
	}
	if options.TokenScopes != nil {
		if err := options.TokenScopes.valid(nil); err == nil {
			for _, scope := range options.TokenScopes {
				queryOptions.Add("tokenScopes", string(scope))
			}
		}
	}
	if options.ForceRename {
		queryOptions.Set("forceRename", "true")
	}

	return queryOptions.Encode()
}
