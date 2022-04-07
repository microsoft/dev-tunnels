package tunnels

import (
	"net/url"
)

// Options that are sent in requests to the tunnels service
type TunnelRequestOptions struct {
	AccessToken       string             // Token used for authentication for service
	AdditionalHeaders map[string]string  //  Additional headers to be included in the request.
	FollowRedirects   bool               // Indicates whether HTTP redirect responses will be automatically followed.
	IncludePorts      bool               // Flag that requests tunnel ports when retrieving a tunnel object.
	Scopes            TunnelAccessScopes // List of scopes that are needed for the current request
	TokenScopes       TunnelAccessScopes // List of token scopes that are requested when retrieving a tunnel or tunnel port object.
	ForceRename       bool               // If there is another tunnel with the name requested in updateTunnel, try to acquire the name from the other tunnel
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
