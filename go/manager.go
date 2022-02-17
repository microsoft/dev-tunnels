package tunnels

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
)

type tokenProviderfn func() string

const (
	apiV1Path                  = "/api/v1"
	tunnelsApiPath             = apiV1Path + "/tunnels"
	subjectsApiPath            = apiV1Path + "/subjects"
	endpointsApiSubPath        = "/endpoints"
	portsApiSubPath            = "/ports"
	tunnelAuthenticationScheme = "Tunnel"
	goUserAgent                = "Visual-Studio-Tunnel-Service-Go-SDK/" + PackageVersion
)

var (
	manageAccessTokenScope       = []TunnelAccessScope{TunnelAccessScopeManage}
	hostAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeHost}
	hostOrManageAccessTokenScope = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost}
	readAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost, TunnelAccessScopeConnect}
)

type Manager struct {
	tokenProvider     tokenProviderfn
	httpClient        *http.Client
	uri               *url.URL
	additionalHeaders map[string]string
	userAgent         string
}

func NewManager(userAgent string, tp tokenProviderfn, tunnelServiceUrl *url.URL, httpHandler *http.Client) (*Manager, error) {
	if len(userAgent) == 0 {
		return nil, fmt.Errorf("userAgent cannot be empty")
	}
	if tp == nil {
		tp = func() string {
			return ""
		}
	}
	var client *http.Client
	if httpHandler == nil {
		client = &http.Client{}

	} else {
		client = httpHandler
	}
	return &Manager{tokenProvider: tp, httpClient: client, uri: tunnelServiceUrl, userAgent: userAgent}, nil
}

func (m *Manager) ListTunnels(
	ctx context.Context, clusterID string, domain string, options *TunnelRequestOptions,
) (ts []*Tunnel, err error) {
	queryParams := url.Values{}
	if len(clusterID) == 0 {
		queryParams.Add("global", "true")
	}
	if len(domain) > 0 {
		queryParams.Add("domain", domain)
	}
	url := m.buildUri(clusterID, tunnelsApiPath, options, queryParams.Encode())
	response, err := m.sendTunnelRequest(ctx, nil, options, http.MethodGet, url, nil, readAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending list tunnel request: %w", err)
	}

	err = json.Unmarshal(response, &ts)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return ts, nil
}

func (m *Manager) SearchTunnels(
	ctx context.Context, tags []string, requireAllTags bool, clusterID string, options *TunnelRequestOptions,
) ([]*TunnelPort, error) {
	return nil, nil
}

func (m *Manager) GetTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, tunnelsApiPath, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, convertedTunnel, readAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &t)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return t, err
}

func (m *Manager) CreateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel must be provided")
	}
	if tunnel.TunnelID != "" {
		return nil, fmt.Errorf("tunnelId cannot be set for creating a tunnel")
	}
	url := m.buildUri(tunnel.ClusterID, tunnelsApiPath, options, "")
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPost, url, convertedTunnel, manageAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending create tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &t)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return t, err
}

func (m *Manager) UpdateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (*Tunnel, error) {
	return nil, nil
}

func (m *Manager) DeleteTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (bool, error) {
	url, err := m.buildTunnelSpecificUri(tunnel, tunnelsApiPath, options, "")
	if err != nil {
		return false, fmt.Errorf("error creating tunnel url: %w", err)
	}
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return false, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, convertedTunnel, manageAccessTokenScope, true)
	if err != nil {
		return false, fmt.Errorf("error sending delete tunnel request: %w", err)
	}

	result := err == nil

	return result, err
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
	ctx context.Context, tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, method string, uri *url.URL, requestObject interface{}, accessTokenScopes []TunnelAccessScope, allowNotFound bool,
) ([]byte, error) {
	tunnelJson, err := json.Marshal(requestObject)
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel to json: %w", err)
	}
	request, err := http.NewRequest(method, uri.String(), bytes.NewBuffer(tunnelJson))
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel request request: %w", err)
	}

	//Add authorization header
	if token := m.getAccessToken(tunnel, tunnelRequestOptions, accessTokenScopes); token != "" {
		request.Header.Add("Authorization", token)
	}
	request.Header.Add("User-Agent", m.userAgent)
	request.Header.Add("User-Agent", goUserAgent)
	request.Header.Add("Content-Type", "application/json;charset=UTF-8")

	// Add additional headers
	for header, headerValue := range m.additionalHeaders {
		request.Header.Add(header, headerValue)
	}
	for header, headerValue := range tunnelRequestOptions.AdditionalHeaders {
		request.Header.Add(header, headerValue)
	}

	result, err := m.httpClient.Do(request)
	if err != nil {
		return nil, fmt.Errorf("error sending request: %w", err)
	}

	// Handle non 200s responses
	if result.StatusCode > 300 {
		return nil, fmt.Errorf("unsuccessful request, response: %d: %s", result.StatusCode, http.StatusText(result.StatusCode))
	}

	defer result.Body.Close()
	return io.ReadAll(result.Body)
}

func (m *Manager) getAccessToken(tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, scopes []TunnelAccessScope) (token string) {
	if tunnelRequestOptions.AccessToken != "" {
		token = tunnelRequestOptions.AccessToken
	}
	if token == "" {
		token = m.tokenProvider()
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

func (m *Manager) buildUri(clusterId string, path string, options *TunnelRequestOptions, query string) *url.URL {
	baseAddress := m.uri
	if clusterId != "" {
		if !strings.HasPrefix(baseAddress.Host, "localhost") && !strings.HasPrefix(baseAddress.Host, clusterId) {
			// A specific cluster ID was specified (while not running on localhost).
			// Prepend the cluster ID to the hostname, and optionally strip a global prefix.
			baseAddress.Host = fmt.Sprintf("%s.%s", clusterId, baseAddress.Host)
			baseAddress.Host = strings.Replace(baseAddress.Host, "global.", "", 1)
		}
	}
	if options != nil {
		optionsQuery := options.queryString()
		if optionsQuery != "" {
			if query != "" {
				query = fmt.Sprintf("%s&%s", query, optionsQuery)
			} else {
				query = optionsQuery
			}
		}
	}
	baseAddress.Path = path
	baseAddress.RawQuery = query
	return baseAddress
}

func (m *Manager) buildTunnelSpecificUri(tunnel *Tunnel, path string, options *TunnelRequestOptions, query string) (*url.URL, error) {
	var tunnelPath string
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel cannot be nil to make uri")
	}
	if tunnel.ClusterID != "" && tunnel.TunnelID != "" {
		tunnelPath = fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.TunnelID)
	} else if tunnel.Name != "" {
		if tunnel.Domain != "" {
			tunnelPath = fmt.Sprintf("%s/%s.%s", tunnelsApiPath, tunnel.Name, tunnel.Domain)
		} else {
			tunnelPath = fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.Name)
		}
	} else {
		return nil, fmt.Errorf("tunnel must have either a name or cluster id and tunnel id")
	}
	return m.buildUri(tunnel.ClusterID, tunnelPath, options, query), nil
}
