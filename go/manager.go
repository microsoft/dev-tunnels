package tunnels

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
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
	ctx context.Context, tags []string, requireAllTags bool, clusterID string, domain string, options *TunnelRequestOptions,
) (ts []*TunnelPort, err error) {
	queryParams := url.Values{}
	if len(clusterID) == 0 {
		queryParams.Add("global", "true")
	}
	if len(domain) > 0 {
		queryParams.Add("domain", domain)
	}
	queryParams.Add("allTags", strconv.FormatBool(requireAllTags))
	tagString := strings.Join(tags, ",")
	queryParams.Add("tags", tagString)

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

func (m *Manager) GetTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, readAccessTokenScope, true)
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

func (m *Manager) UpdateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel must be provided")
	}

	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating request url: %w", err)
	}

	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, convertedTunnel, manageAccessTokenScope, false)
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

func (m *Manager) DeleteTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) error {
	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return fmt.Errorf("error converting tunnel for request: %w", err)
	}
	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, convertedTunnel, manageAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending delete tunnel request: %w", err)
	}

	return err
}

func (m *Manager) UpdateTunnelEndpoint(
	ctx context.Context, tunnel *Tunnel, endpoint *TunnelEndpoint, options *TunnelRequestOptions,
) (te *TunnelEndpoint, err error) {
	if endpoint == nil {
		return nil, fmt.Errorf("endpoint must be provided and must not be nil")
	}
	if endpoint.HostID == "" {
		return nil, fmt.Errorf("endpoint hostId must be provided and must not be nil")
	}
	url, err := m.buildTunnelSpecificUri(tunnel, fmt.Sprintf("%s/%s/%s", endpointsApiSubPath, endpoint.HostID, endpoint.ConnectionMode), options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, endpoint, hostAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending delete tunnel request: %w", err)
	}

	// Read response into a tunnel endpoint
	err = json.Unmarshal(response, &te)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	if tunnel.Endpoints != nil {
		newEndpoints := make([]*TunnelEndpoint, 0)
		for _, ep := range tunnel.Endpoints {
			if ep.HostID != endpoint.HostID || ep.ConnectionMode != endpoint.ConnectionMode {
				newEndpoints = append(newEndpoints, ep)
			}
		}
		newEndpoints = append(newEndpoints, te)
		tunnel.Endpoints = newEndpoints
	}
	return te, err
}

func (m *Manager) DeleteTunnelEndpoints(
	ctx context.Context, tunnel *Tunnel, hostID string, connectionMode TunnelConnectionMode, options *TunnelRequestOptions,
) error {
	if hostID == "" {
		return fmt.Errorf("hostId must be provided and must not be nil")
	}
	var path string
	if connectionMode == "" {
		path = fmt.Sprintf("%s/%s", endpointsApiSubPath, hostID)
	} else {
		path = fmt.Sprintf("%s/%s/%s", endpointsApiSubPath, hostID, connectionMode)
	}
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}

	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, nil, hostAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending endpoint delete tunnel request: %w", err)
	}

	if tunnel.Endpoints != nil {
		newEndpoints := make([]*TunnelEndpoint, 0)
		for _, ep := range tunnel.Endpoints {
			if ep.HostID != hostID || ep.ConnectionMode != connectionMode {
				newEndpoints = append(newEndpoints, ep)
			}
		}
		tunnel.Endpoints = newEndpoints
	}
	return err
}

func (m *Manager) ListTunnelPorts(
	ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions,
) (tp []*TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, portsApiSubPath, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, readAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel ports: %w", err)
	}
	return tp, nil
}

func (m *Manager) GetTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, fmt.Sprintf("%s/%d", portsApiSubPath, port), options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, readAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel ports: %w", err)
	}
	return tp, nil
}

func (m *Manager) CreateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, portsApiSubPath, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	convertedPort, err := port.requestObject(tunnel)
	if err != nil {
		return nil, fmt.Errorf("error converting port for request: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPost, url, convertedPort, hostOrManageAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel port: %w", err)
	}

	// Updated local tunnel ports
	if tunnel.Ports != nil {
		newPorts := make([]*TunnelPort, 0)
		for _, p := range tunnel.Ports {
			if p.PortNumber != tp.PortNumber {
				newPorts = append(newPorts, p)
			}
		}
		newPorts = append(newPorts, tp)
		tunnel.Ports = newPorts
	} else {
		tunnel.Ports = make([]*TunnelPort, 1)
		tunnel.Ports[0] = tp
	}
	return tp, nil
}

func (m *Manager) UpdateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	if port.ClusterID != "" && tunnel.ClusterID != "" && port.ClusterID != tunnel.ClusterID {
		return nil, fmt.Errorf("cluster ids do not match")
	}
	path := fmt.Sprintf("%s/%d", portsApiSubPath, port.PortNumber)
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	convertedPort, err := port.requestObject(tunnel)
	if err != nil {
		return nil, fmt.Errorf("error converting port for request: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, convertedPort, hostOrManageAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel port: %w", err)
	}

	// Updated local tunnel ports
	if tunnel.Ports != nil {
		newPorts := make([]*TunnelPort, 0)
		for _, p := range tunnel.Ports {
			if p.PortNumber != tp.PortNumber {
				newPorts = append(newPorts, p)
			}
		}
		newPorts = append(newPorts, tp)
		tunnel.Ports = newPorts
	}
	return tp, nil
}

func (m *Manager) DeleteTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) error {

	path := fmt.Sprintf("%s/%d", portsApiSubPath, port)
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}

	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, nil, hostOrManageAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Updated local tunnel ports
	if tunnel.Ports != nil {
		newPorts := make([]*TunnelPort, 0)
		for _, p := range tunnel.Ports {
			if p.PortNumber != port {
				newPorts = append(newPorts, p)
			}
		}
		tunnel.Ports = newPorts
	}
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
	if token == "" && tunnel != nil && tunnel.AccessTokens != nil {
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
	return m.buildUri(tunnel.ClusterID, tunnelPath+path, options, query), nil
}
