package tunnels

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"
	"net/url"
	"reflect"
)

type fn func() string

const (
	apiV1Path                  = "/api/v1"
	tunnelsApiPath             = apiV1Path + "/tunnels"
	subjectsApiPath            = apiV1Path + "/subjects"
	endpointsApiSubPath        = "/endpoints"
	portsApiSubPath            = "/ports"
	tunnelAuthenticationScheme = "Tunnel"
)

var (
	manageAccessTokenScope       = []TunnelAccessScope{TunnelAccessScopeManage}
	hostAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeHost}
	hostOrManageAccessTokenScope = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost}
	readAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost, TunnelAccessScopeConnect}
)

type Manager struct {
	tokenProvider     fn
	httpClient        *http.Client
	uri               *url.URL
	additionalHeaders map[string]string
	userAgent         string
}

func NewManager(userAgent string, accessTokenCallback fn, tunnelServiceUrl *url.URL, httpHandler *http.Client) (*Manager, error) {
	if len(userAgent) == 0 {
		return nil, fmt.Errorf("userAgent cannot be empty")
	}
	if accessTokenCallback == nil {
		accessTokenCallback = func() string {
			return ""
		}
	}
	var client *http.Client
	if httpHandler == nil {
		client = &http.Client{}

	} else {
		client = httpHandler
	}
	return &Manager{tokenProvider: accessTokenCallback, httpClient: client, uri: tunnelServiceUrl, userAgent: userAgent}, nil
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
	if tunnel.TunnelID != "" {
		return nil, fmt.Errorf("tunnelId cannot be set for creating a tunnel")
	}
	url := m.buildUri(tunnel.ClusterID, tunnelsApiPath, options, "")
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	createdTunnelResult := &Tunnel{}
	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodPost, url, convertedTunnel, manageAccessTokenScope, false, createdTunnelResult)
	return createdTunnelResult, err
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
	ctx context.Context, tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, method string, uri url.URL, requestObject interface{}, accessTokenScopes []TunnelAccessScope, allowNotFound bool, resultObj interface{},
) (resp interface{}, err error) {
	tunnelJson, err := json.Marshal(requestObject)
	if err != nil {
		return nil, err
	}
	request, err := http.NewRequest(method, uri.String(), bytes.NewBuffer(tunnelJson))
	if err != nil {
		return nil, err
	}

	//Add authorization header
	if token := m.getAccessToken(tunnel, tunnelRequestOptions, accessTokenScopes); token != "" {
		request.Header.Add("Authorization", token)
	}
	request.Header.Add("User-Agent", m.userAgent)
	request.Header.Add("Content-Type", "application/json;charset=UTF-8")

	// Add additional headers
	for header, headerValue := range m.additionalHeaders {
		request.Header.Add(header, headerValue)
	}
	for header, headerValue := range tunnelRequestOptions.AdditionalHeaders {
		request.Header.Add(header, headerValue)
	}

	if err != nil {
		return nil, err
	}

	result, err := m.httpClient.Do(request)
	if err != nil {
		return nil, err
	}

	convertedResponse, err := convertResponse(ctx, result, allowNotFound, resultObj)
	return convertedResponse, err
}

func convertResponse(ctx context.Context, resp *http.Response, allowNotFound bool, resultObj interface{}) (interface{}, error) {
	if resp == nil {
		return nil, fmt.Errorf("reponse cannot be nil")
	}
	defer resp.Body.Close()
	// If response is a 2XX success
	if resp.StatusCode/100 == 2 {
		if resp.StatusCode == http.StatusNoContent || resp.Body == nil {
			if reflect.TypeOf(resultObj).Kind() == reflect.Bool {
				return true, nil
			}
			return nil, nil
		}
		jsonDataFromHttp, err := ioutil.ReadAll(resp.Body)
		if err != nil {
			return nil, err
		}
		err = json.Unmarshal(jsonDataFromHttp, &resultObj)
		if err != nil {
			return nil, err
		}
		return resultObj, nil
	}
	switch resp.StatusCode {
	case http.StatusBadRequest:
		return nil, fmt.Errorf("response 400: bad request")
	case http.StatusUnauthorized:
		return nil, fmt.Errorf("response 401: unauthorized")
	case http.StatusForbidden:
		return nil, fmt.Errorf("response 403: forbidden")
	case http.StatusConflict:
		return nil, fmt.Errorf("response 409: conflict ")
	default:
		return nil, fmt.Errorf(resp.Status)
	}
}

func (m *Manager) getAccessToken(tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, scopes []TunnelAccessScope) string {
	var token string
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

func (m *Manager) buildUri(clusterId string, path string, options *TunnelRequestOptions, query string) url.URL {
	baseAddress := m.uri
	if clusterId != "" {
		//TODO
	}
	if options != nil {
		optionsQuery := options.toQueryString()
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
	return *baseAddress
}
