package tunnels

import (
	"context"
	"fmt"
	"net/http"
	"net/url"
	"strings"
)

type fn func() <-chan string

const (
	apiV1Path                  = "/api/v1"
	tunnelsApiPath             = apiV1Path + "/tunnels"
	subjectsApiPath            = apiV1Path + "/subjects"
	endpointsApiSubPath        = "/endpoints"
	portsApiSubPath            = "/ports"
	tunnelAuthenticationScheme = "Tunnel"
)

var (
	manageAccessTokenScope       = []string{ManageScope}
	hostAccessTokenScope         = []string{HostScope}
	hostOrManageAccessTokenScope = []string{ManageScope, HostScope}
	readAccessTokenScope         = []string{ManageScope, HostScope, ConnectScope}
)

type Manager struct {
	accessTokenCallback fn
	httpClient          *http.Client
	uri                 url.URL
	additionalHeaders   map[string]string
	userAgent           string
}

func NewManager(userAgent string, accessTokenCallback fn, tunnelServiceUrl url.URL, httpHandler *http.Client) (*Manager, error) {
	if isEmpty(userAgent) {
		return nil, fmt.Errorf("userAgent cannot be empty")
	}
	if accessTokenCallback == nil {
		accessTokenCallback = func() <-chan string {
			r := make(chan string)
			defer close(r)
			r <- ""
			return r
		}
	}
	var client *http.Client
	if httpHandler == nil {
		client = &http.Client{}

	} else {
		client = httpHandler
	}
	return &Manager{accessTokenCallback: accessTokenCallback, httpClient: client, uri: tunnelServiceUrl}, nil
}

type TunnelRequestOptions struct {
	AccessToken       string
	AdditionalHeaders map[string]string
	FollowRedirects   bool
	IncludePorts      bool
	Scopes            []string
	TokenScopes       []string
}

func (m *Manager) ListTunnels(
	ctx context.Context, clusterID string, options *TunnelRequestOptions,
) ([]*Tunnel, error) {
	return nil, nil
}

func (m *Manager) SearchTunnels(
	ctx context.Context, tags []string, requireAllTags bool, clusterID string, options *TunnelRequestOptions,
) ([]*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) GetTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) CreateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel must be provided")
	}
	if tunnelId := tunnel.TunnelID; tunnelId != "" {
		return nil, fmt.Errorf("tunnelId cannot be set for creating a tunnel")
	}
	return nil, nil
}

func (m *Manager) UpdateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (bool, error) {
	return false, nil
}

func (m *Manager) UpdateTunnelEndpoint(
	ctx context.Context, tunnel *Tunnel, endpoint *TunnelEndpoint, options *TunnelRequestOptions,
) (*TunnelEndpoint, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnelEndpoints(
	ctx context.Context, tunnel *Tunnel, hostID string, connectionMode TunnelConnectionMode, options *TunnelRequestOptions,
) error {
	return nil
}

func (m *Manager) ListTunnelPorts(
	ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions,
) ([]*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) GetTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) CreateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) UpdateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) error {
	return nil
}

func (m *Manager) sendTunnelRequest(
	ctx context.Context, tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, method string, uri url.URL, requestObject url.Values, accessTokenScopes []string, allowNotFound bool,
) (resp *http.Response, err error) {
	headers := url.Values{}

	//Add authorization header
	headers.Add("Authorization", m.getAccessToken(tunnel, tunnelRequestOptions, accessTokenScopes))
	headers.Add("User-Agent", m.userAgent)

	// Add additional headers
	for header, headerValue := range m.additionalHeaders {
		headers.Add(header, headerValue)
	}
	for header, headerValue := range tunnelRequestOptions.AdditionalHeaders {
		headers.Add(header, headerValue)
	}

	request, err := http.NewRequest(method, uri.String(), strings.NewReader(headers.Encode()))
	if err != nil {
		return nil, err
	}
	result, err := m.httpClient.Do(request)
	return result, err
}

func (m *Manager) getAccessToken(tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, scopes []string) string {
	var token string
	if tunnelRequestOptions.AccessToken != "" {
		token = tunnelRequestOptions.AccessToken
	}
	if token == "" {
		token = <-m.accessTokenCallback()
	}
	if token == "" && tunnel.AccessTokens != nil {
		for _, scope := range scopes {
			if tunnelToken, ok := tunnel.AccessTokens[scope]; ok {
				token = fmt.Sprintf("%s %s", tunnelAuthenticationScheme, tunnelToken)
			}
		}
	}
	return token
}

func (m *Manager) buildUri(clusterId string, path string, options TunnelRequestOptions, query string) url.URL {
	baseAddress := m.uri
}
